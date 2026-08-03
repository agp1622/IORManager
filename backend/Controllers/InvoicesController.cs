using System.IO.Compression;
using System.Linq;
using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using IORManager.Services.Dgii;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Invoice> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly INcfNumberGenerator _ncfNumberGenerator;
    private readonly IEcfNumberGenerator _ecfNumberGenerator;
    private readonly IEcfService _ecfService;
    private readonly IORManagerContext _context;

    public InvoicesController(
        IFinancialDocumentRepository<Invoice> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator,
        INcfNumberGenerator ncfNumberGenerator,
        IEcfNumberGenerator ecfNumberGenerator,
        IEcfService ecfService,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
        _ncfNumberGenerator = ncfNumberGenerator;
        _ecfNumberGenerator = ecfNumberGenerator;
        _ecfService = ecfService;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Invoice>> GetInvoices()
    {
        var invoices = _repository.GetAll().ToList();
        PopulateQuoteNumbers(invoices);
        PopulateCustomerPaymentTerms(invoices);
        PopulateEcfStatus(invoices);
        return Ok(invoices);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<Invoice> GetInvoice(Guid id)
    {
        var invoice = _repository.GetById(id);
        if (invoice is null)
        {
            return NotFound();
        }

        PopulateQuoteNumbers([invoice]);
        PopulateCustomerPaymentTerms([invoice]);
        PopulateEcfStatus([invoice]);
        return Ok(invoice);
    }

    /// <summary>Lists soft-deleted invoices still inside their 1-year recovery window (the "trash").</summary>
    [HttpGet("trash")]
    [Authorize(Roles = "Admin")]
    public ActionResult<IReadOnlyCollection<Invoice>> GetTrashedInvoices()
    {
        var cutoff = DateTime.UtcNow - FinancialDocument.SoftDeleteRecoveryWindow;
        var invoices = _context.Invoices
            .IgnoreQueryFilters()
            .Include(invoice => invoice.Lines)
            .Where(invoice => invoice.DeletedAt != null && invoice.DeletedAt >= cutoff)
            .OrderByDescending(invoice => invoice.DeletedAt)
            .ToList();

        PopulateQuoteNumbers(invoices);
        PopulateCustomerPaymentTerms(invoices);
        return Ok(invoices);
    }

    /// <summary>Soft-deletes an invoice. It remains recoverable for 1 year via <see cref="RestoreInvoice"/>.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public ActionResult DeleteInvoice(Guid id)
    {
        var invoice = _context.Invoices.FirstOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        invoice.DeletedAt = DateTime.UtcNow;
        _context.SaveChanges();
        return NoContent();
    }

    /// <summary>Restores a soft-deleted invoice, provided its recovery window hasn't expired.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    public ActionResult<Invoice> RestoreInvoice(Guid id)
    {
        var invoice = _context.Invoices
            .IgnoreQueryFilters()
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (invoice is null || invoice.DeletedAt is null)
        {
            return NotFound();
        }

        var cutoff = DateTime.UtcNow - FinancialDocument.SoftDeleteRecoveryWindow;
        if (invoice.DeletedAt < cutoff)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Recovery window expired",
                Detail = "This invoice was deleted more than a year ago and can no longer be restored.",
            });
        }

        invoice.DeletedAt = null;
        _context.SaveChanges();
        return Ok(invoice);
    }

    /// <summary>Marks an invoice as paid by the customer.</summary>
    [HttpPost("{id:guid}/mark-paid")]
    public ActionResult<Invoice> MarkInvoicePaid(Guid id)
    {
        var invoice = _context.Invoices.FirstOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        invoice.PaidAt ??= DateTime.UtcNow;
        _context.SaveChanges();
        return Ok(invoice);
    }

    /// <summary>Reverts an invoice back to unpaid — useful if it was marked paid by mistake.</summary>
    [HttpPost("{id:guid}/mark-unpaid")]
    public ActionResult<Invoice> MarkInvoiceUnpaid(Guid id)
    {
        var invoice = _context.Invoices.FirstOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        invoice.PaidAt = null;
        _context.SaveChanges();
        return Ok(invoice);
    }

    /// <summary>
    /// Marks an invoice as sent/delivered to the customer. This starts the payment-due countdown (sent date plus
    /// the customer's payment terms), which is how the "payment due" alert is triggered later.
    /// </summary>
    [HttpPost("{id:guid}/mark-sent")]
    public ActionResult<Invoice> MarkInvoiceSent(Guid id)
    {
        var invoice = _context.Invoices.FirstOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        invoice.SentAt ??= DateTime.UtcNow;
        _context.SaveChanges();

        PopulateCustomerPaymentTerms([invoice]);
        return Ok(invoice);
    }

    /// <summary>Reverts an invoice back to not-sent — useful if it was marked sent by mistake.</summary>
    [HttpPost("{id:guid}/mark-unsent")]
    public ActionResult<Invoice> MarkInvoiceUnsent(Guid id)
    {
        var invoice = _context.Invoices.FirstOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        invoice.SentAt = null;
        _context.SaveChanges();
        return Ok(invoice);
    }

    /// <summary>Combines the selected invoices into a single downloadable PDF, one invoice per page.</summary>
    [HttpPost("batch-pdf")]
    public ActionResult DownloadInvoicesBatchPdf([FromBody] InvoiceBatchRequest request)
    {
        if (request?.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No invoices selected",
                Detail = "Provide at least one invoice id.",
            });
        }

        var invoices = _context.Invoices
            .Include(invoice => invoice.Lines)
            .Where(invoice => request.Ids.Contains(invoice.Id))
            .ToList();

        // Preserve the order the caller selected them in, rather than whatever order the DB returns.
        var orderedInvoices = request.Ids
            .Select(id => invoices.FirstOrDefault(invoice => invoice.Id == id))
            .Where(invoice => invoice is not null)
            .Select(invoice => invoice!)
            .ToList();

        if (orderedInvoices.Count == 0)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateInvoicesBatchPdf(orderedInvoices);
        var fileName = orderedInvoices.Count == 1
            ? $"Invoice-{DocumentNumberFormatter.ToInvoiceNumber(orderedInvoices[0].Number)}.pdf"
            : $"Invoices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>Bundles the selected invoices as individual PDFs inside a single ZIP file.</summary>
    [HttpPost("batch-zip")]
    public ActionResult DownloadInvoicesBatchZip([FromBody] InvoiceBatchRequest request)
    {
        if (request?.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No invoices selected",
                Detail = "Provide at least one invoice id.",
            });
        }

        var invoices = _context.Invoices
            .Include(invoice => invoice.Lines)
            .Where(invoice => request.Ids.Contains(invoice.Id))
            .ToList();

        if (invoices.Count == 0)
        {
            return NotFound();
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var invoice in invoices)
            {
                var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, invoice.NcfNumber);
                var baseName = $"Invoice-{DocumentNumberFormatter.ToInvoiceNumber(invoice.Number)}";
                var entryName = $"{baseName}.pdf";
                var suffix = 2;
                while (!usedNames.Add(entryName))
                {
                    entryName = $"{baseName}-{suffix}.pdf";
                    suffix++;
                }

                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(pdfBytes, 0, pdfBytes.Length);
            }
        }

        zipStream.Position = 0;
        var zipFileName = $"Invoices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return File(zipStream.ToArray(), "application/zip", zipFileName);
    }

    [HttpGet("ncf-categories")]
    public ActionResult<IReadOnlyCollection<NcfCategoryResponse>> GetNcfCategories()
    {
        var categories = NcfCategoryCatalog.GetAll()
            .Select(definition => new NcfCategoryResponse(
                definition.Code,
                definition.Name,
                definition.SequenceLength,
                definition.IsElectronic))
            .ToArray();

        return Ok(categories);
    }

    [HttpGet("fiscal-regimes")]
    public ActionResult<IReadOnlyCollection<FiscalRegimeResponse>> GetFiscalRegimes()
    {
        var regimes = _context.FiscalRegimes
            .OrderBy(r => r.Id)
            .ToList();

        var responses = regimes.Select(regime =>
        {
            // Last NCF used: latest NcfNumber in this category across ALL invoices
            // (uses NcfCategory so pre-existing invoices without FiscalRegimeId are included)
            var lastNcf = _context.Invoices
                .Where(i => i.NcfCategory == regime.Code && i.NcfNumber != null)
                .OrderByDescending(i => i.NcfNumber)
                .Select(i => i.NcfNumber)
                .FirstOrDefault();

            // Next NCF: peek without advancing the sequence
            var nextNcf = PeekNcf(regime.Code);

            return new FiscalRegimeResponse(
                regime.Id,
                regime.Code,
                regime.Name,
                regime.InvoiceCount,
                lastNcf,
                nextNcf);
        }).ToList();

        return Ok(responses);
    }

    [HttpGet("next-ncf")]
    public ActionResult<NcfAssignmentResponse> GetNextNcf([FromQuery] string? ncfCategory)
    {
        if (!TryNormalizeCategoryCode(ncfCategory, out var normalizedCategory, out var categoryError))
        {
            return categoryError!;
        }

        normalizedCategory ??= NcfCategoryCatalog.DefaultCategoryCode;
        var nextNumber = PeekNcf(normalizedCategory);
        if (nextNumber is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No RFCE range configured",
                Detail = $"No DGII-authorized numbering range is configured for \"{normalizedCategory}\" yet.",
            });
        }

        return Ok(new NcfAssignmentResponse(nextNumber, normalizedCategory));
    }

    [HttpPut("ncf-sequences/{categoryCode}")]
    public ActionResult SetNcfSequence(string categoryCode, [FromBody] NcfSequenceSetRequest request)
    {
        if (!NcfCategoryCatalog.TryGetByCode(categoryCode, out _))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid NCF category",
                Detail = $"The NCF category \"{categoryCode}\" is not supported.",
            });
        }

        if (request.NextNumber < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid next number",
                Detail = "The next number must be at least 1.",
            });
        }

        try
        {
            _ncfNumberGenerator.SetNextNumber(categoryCode, request.NextNumber);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }

        return NoContent();
    }

    /// <summary>Lists the DGII-authorized RFCE numbering ranges configured for each electronic NCF category.</summary>
    [HttpGet("ecf-ranges")]
    public ActionResult<IReadOnlyCollection<EcfRangeResponse>> GetEcfRanges()
    {
        var ranges = _context.EcfRanges.ToDictionary(range => range.DocumentTypeCode);

        var responses = NcfCategoryCatalog.GetAll()
            .Where(definition => definition.IsElectronic)
            .Select(definition =>
            {
                var documentTypeCode = definition.Code[1..];
                ranges.TryGetValue(documentTypeCode, out var range);
                return new EcfRangeResponse(
                    definition.Code,
                    documentTypeCode,
                    range?.RangeStart,
                    range?.RangeEnd,
                    range?.NextNumber,
                    range?.AuthorizedAt,
                    range?.ExpiresAt,
                    PeekNcf(definition.Code));
            })
            .ToList();

        return Ok(responses);
    }

    /// <summary>Records a DGII-authorized RFCE numbering range for one electronic NCF category (e.g. "E31").</summary>
    [HttpPut("ecf-ranges/{categoryCode}")]
    [Authorize(Roles = "Admin")]
    public ActionResult SetEcfRange(string categoryCode, [FromBody] EcfRangeSetRequest request)
    {
        if (!NcfCategoryCatalog.TryGetByCode(categoryCode, out var definition) || !definition.IsElectronic)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid e-CF category",
                Detail = $"\"{categoryCode}\" is not an electronic NCF category.",
            });
        }

        try
        {
            _ecfNumberGenerator.SetRange(categoryCode, request.RangeStart, request.RangeEnd, request.AuthorizedAt, request.ExpiresAt);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }

        return NoContent();
    }

    /// <summary>Builds, signs, and submits the e-CF for this invoice to the DGII.</summary>
    [HttpPost("{id:guid}/ecf/emit")]
    public async Task<ActionResult<EcfSubmissionResponse>> EmitEcf(Guid id)
    {
        try
        {
            var submission = await _ecfService.EmitAsync(id);
            return Ok(ToEcfSubmissionResponse(submission));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "No se pudo emitir el e-CF", Detail = ex.Message });
        }
    }

    /// <summary>Re-queries the DGII for the current validation status of this invoice's e-CF.</summary>
    [HttpPost("{id:guid}/ecf/status/refresh")]
    public async Task<ActionResult<EcfSubmissionResponse>> RefreshEcfStatus(Guid id)
    {
        try
        {
            var submission = await _ecfService.RefreshStatusAsync(id);
            return Ok(ToEcfSubmissionResponse(submission));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "No se pudo consultar el estado", Detail = ex.Message });
        }
    }

    /// <summary>Returns the last known e-CF status for this invoice without contacting the DGII.</summary>
    [HttpGet("{id:guid}/ecf/status")]
    public ActionResult<EcfSubmissionResponse> GetEcfStatus(Guid id)
    {
        var submission = _context.EcfSubmissions.FirstOrDefault(existing => existing.InvoiceId == id);
        if (submission is null)
        {
            return NotFound();
        }

        return Ok(ToEcfSubmissionResponse(submission));
    }

    /// <summary>Downloads the signed e-CF XML for this invoice.</summary>
    [HttpGet("{id:guid}/ecf/xml")]
    public ActionResult GetEcfXml(Guid id)
    {
        var submission = _context.EcfSubmissions.FirstOrDefault(existing => existing.InvoiceId == id);
        if (submission?.SignedXml is null)
        {
            return NotFound();
        }

        return File(System.Text.Encoding.UTF8.GetBytes(submission.SignedXml), "application/xml", $"{submission.ENcf}.xml");
    }

    private static EcfSubmissionResponse ToEcfSubmissionResponse(EcfSubmission submission) => new(
        submission.InvoiceId,
        submission.ENcf,
        submission.DocumentTypeCode,
        submission.Status,
        submission.TrackId,
        submission.SecurityCode,
        submission.ResponseMessage,
        submission.SignedAt,
        submission.SubmittedAt,
        submission.RespondedAt);

    [HttpPost]
    public ActionResult<InvoiceCreateResponse> CreateInvoice([FromBody] InvoiceCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryResolveCustomerRecord(
                request.CustomerName,
                request.CustomerAddress,
                request.CustomerContact,
                out var customer,
                out var customerError))
        {
            return customerError!;
        }

        var invoice = request.ToInvoice();
        invoice.Date = DateOnly.FromDateTime(DateTime.UtcNow);
        invoice.CustomerId = customer.Id;
        invoice.CustomerName = customer.Name;
        invoice.CustomerAddress = customer.Address;
        invoice.CustomerContact = customer.Contact;
        invoice.NcfNumber = null;
        invoice.NcfCategory = null;
        invoice.InvoiceGeneratedAt = null;
        invoice.Number = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber());

        var savedInvoice = _repository.Add(invoice);
        var pdfBytes = _pdfService.GenerateQuotePdf(savedInvoice);
        var response = new InvoiceCreateResponse(
            savedInvoice,
            $"Quote-{savedInvoice.Number}.pdf",
            Convert.ToBase64String(pdfBytes));

        return CreatedAtAction(nameof(GetInvoice), new { id = savedInvoice.Id }, response);
    }

    [HttpPost("{id:guid}/duplicate")]
    public ActionResult<Invoice> DuplicateInvoice(Guid id)
    {
        var source = GetInvoiceWithLines(id);
        if (source is null)
        {
            return NotFound();
        }

        var duplicated = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber()),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            CustomerId = source.CustomerId,
            CustomerName = source.CustomerName,
            CustomerAddress = source.CustomerAddress,
            CustomerContact = source.CustomerContact,
            CurrencyCode = source.CurrencyCode,
            CultureName = source.CultureName,
            ItbisRate = source.ItbisRate,
            NcfNumber = null,
            NcfCategory = null,
            InvoiceGeneratedAt = null,
            Lines = source.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };

        duplicated.RecalculateTotal();
        _context.Invoices.Add(duplicated);
        _context.SaveChanges();

        return CreatedAtAction(nameof(GetInvoice), new { id = duplicated.Id }, duplicated);
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetInvoicePdf(Guid id)
    {
        var invoice = _repository.GetById(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, invoice.NcfNumber);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(pdfBytes, "application/pdf", $"Invoice-{invoiceNumber}.pdf");
    }

    [HttpGet("{id:guid}/invoice-pdf")]
    public ActionResult GetInvoiceDocumentPdf(
        Guid id,
        [FromQuery] string? ncfNumber,
        [FromQuery] string? ncfCategory)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!TryAssignNcfNumber(
                invoice,
                ncfNumber,
                ncfCategory,
                out var normalizedNcf,
                out _,
                out var errorResult))
        {
            return errorResult!;
        }

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, normalizedNcf);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(pdfBytes, "application/pdf", $"Invoice-{invoiceNumber}.pdf");
    }

    [HttpPut("{id:guid}")]
    public ActionResult<Invoice> UpdateInvoice(Guid id, [FromBody] InvoiceUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invoice = _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        invoice.CustomerName = request.CustomerName;
        invoice.CustomerAddress = request.CustomerAddress.Trim();
        invoice.CustomerContact = request.CustomerContact.Trim();
        invoice.CurrencyCode = request.CurrencyCode;
        invoice.CultureName = request.Locale;
        invoice.ItbisRate = request.ItbisRate;
        if (!TryResolveCustomerRecord(
                invoice.CustomerName,
                invoice.CustomerAddress,
                invoice.CustomerContact,
                out var customer,
                out var customerError))
        {
            return customerError!;
        }

        invoice.CustomerId = customer.Id;
        invoice.CustomerName = customer.Name;
        invoice.CustomerAddress = customer.Address;
        invoice.CustomerContact = customer.Contact;

        var existingLines = invoice.Lines.ToList();
        if (existingLines.Count > 0)
        {
            _context.DocumentLines.RemoveRange(existingLines);
        }
        invoice.Lines.Clear();

        foreach (var lineRequest in request.Lines)
        {
            invoice.Lines.Add(lineRequest.ToDocumentLine());
        }

        invoice.RecalculateTotal();
        _context.SaveChanges();

        return Ok(invoice);
    }

    private Invoice? GetInvoiceWithLines(Guid id) =>
        _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

    /// <summary>Routes NCF generation to the e-CF (DGII RFCE range) generator for electronic categories.</summary>
    private string GenerateNcf(string categoryCode) =>
        NcfCategoryCatalog.TryGetByCode(categoryCode, out var definition) && definition.IsElectronic
            ? _ecfNumberGenerator.GenerateNextNumber(categoryCode)
            : _ncfNumberGenerator.GenerateNextNumber(categoryCode);

    /// <summary>Same routing as <see cref="GenerateNcf"/> but without advancing the sequence.</summary>
    private string? PeekNcf(string categoryCode) =>
        NcfCategoryCatalog.TryGetByCode(categoryCode, out var definition) && definition.IsElectronic
            ? _ecfNumberGenerator.PeekNextNumber(categoryCode)
            : _ncfNumberGenerator.PeekNextNumber(categoryCode);

    private void PopulateQuoteNumbers(IEnumerable<Invoice> invoices)
    {
        var invoiceList = invoices.ToList();
        var quoteIds = invoiceList
            .Where(invoice => invoice.QuoteId.HasValue)
            .Select(invoice => invoice.QuoteId!.Value)
            .Distinct()
            .ToList();

        if (quoteIds.Count == 0)
        {
            return;
        }

        var quoteNumbers = _context.Quotes
            .AsNoTracking()
            .Where(quote => quoteIds.Contains(quote.Id))
            .Select(quote => new { quote.Id, quote.Number })
            .ToDictionary(quote => quote.Id, quote => quote.Number);

        foreach (var invoice in invoiceList)
        {
            if (!invoice.QuoteId.HasValue)
            {
                invoice.QuoteNumber = null;
                continue;
            }

            invoice.QuoteNumber = quoteNumbers.TryGetValue(invoice.QuoteId.Value, out var quoteNumber)
                ? quoteNumber
                : null;
        }
    }

    /// <summary>
    /// Populates each invoice's <see cref="Invoice.CustomerPaymentTermsDays"/> from its linked customer, so
    /// <see cref="Invoice.PaymentDueDate"/> and <see cref="Invoice.IsPaymentDue"/> can be computed client-side
    /// without a separate lookup. Mirrors the <see cref="PopulateQuoteNumbers"/> pattern above.
    /// </summary>
    private void PopulateCustomerPaymentTerms(IEnumerable<Invoice> invoices)
    {
        var invoiceList = invoices.ToList();
        var customerIds = invoiceList
            .Where(invoice => invoice.CustomerId.HasValue)
            .Select(invoice => invoice.CustomerId!.Value)
            .Distinct()
            .ToList();

        if (customerIds.Count == 0)
        {
            return;
        }

        var paymentTerms = _context.Customers
            .AsNoTracking()
            .Where(customer => customerIds.Contains(customer.Id))
            .Select(customer => new { customer.Id, customer.DefaultPaymentTermsDays })
            .ToDictionary(customer => customer.Id, customer => customer.DefaultPaymentTermsDays);

        foreach (var invoice in invoiceList)
        {
            if (invoice.CustomerId.HasValue && paymentTerms.TryGetValue(invoice.CustomerId.Value, out var days))
            {
                invoice.CustomerPaymentTermsDays = days;
            }
        }
    }

    /// <summary>Populates each invoice's e-CF status fields from its <see cref="EcfSubmission"/>, if any.</summary>
    private void PopulateEcfStatus(IEnumerable<Invoice> invoices)
    {
        var invoiceList = invoices.ToList();
        var invoiceIds = invoiceList.Select(invoice => invoice.Id).ToList();
        if (invoiceIds.Count == 0)
        {
            return;
        }

        var submissions = _context.EcfSubmissions
            .AsNoTracking()
            .Where(submission => invoiceIds.Contains(submission.InvoiceId))
            .ToDictionary(submission => submission.InvoiceId);

        foreach (var invoice in invoiceList)
        {
            if (!submissions.TryGetValue(invoice.Id, out var submission))
            {
                continue;
            }

            invoice.EcfStatus = submission.Status;
            invoice.EcfTrackId = submission.TrackId;
            invoice.EcfSecurityCode = submission.SecurityCode;
            invoice.EcfResponseMessage = submission.ResponseMessage;
        }
    }

    [HttpPost("{id:guid}/ncf")]
    public ActionResult<NcfAssignmentResponse> AssignNcf(Guid id, [FromBody] NcfAssignmentRequest? request)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

        // Capture before TryAssignNcfNumber sets it — used to detect first-time assignment.
        var isFirstAssignment = !invoice.InvoiceGeneratedAt.HasValue;

        if (!TryAssignNcfNumber(
                invoice,
                request?.NcfNumber,
                request?.NcfCategory,
                out var normalizedNcf,
                out var normalizedCategory,
                out var errorResult))
        {
            return errorResult!;
        }

        // Auto-create an AccountPayable the first time an invoice gets its NCF.
        if (isFirstAssignment && !_context.AccountsPayable.Any(ap => ap.InvoiceId == invoice.Id))
        {
            var paymentTermsDays = invoice.CustomerId.HasValue
                ? _context.Customers
                    .Where(c => c.Id == invoice.CustomerId.Value)
                    .Select(c => (int?)c.DefaultPaymentTermsDays)
                    .FirstOrDefault() ?? 30
                : 30;

            _context.AccountsPayable.Add(new AccountPayable
            {
                Id = Guid.NewGuid(),
                Number = $"AP-{invoice.Number}",
                SupplierName = invoice.CustomerName ?? invoice.PartyName,
                TotalAmount = invoice.TotalAmount,
                CurrencyCode = invoice.CurrencyCode,
                CultureName = invoice.CultureName,
                Date = invoice.Date,
                DueDate = invoice.Date.AddDays(paymentTermsDays),
                Status = "Pendiente",
                InvoiceId = invoice.Id,
            });
            _context.SaveChanges();
        }

        return Ok(new NcfAssignmentResponse(
            normalizedNcf!,
            normalizedCategory ?? NcfCategoryCatalog.DefaultCategoryCode));
    }

    private bool TryAssignNcfNumber(
        Invoice invoice,
        string? requestedNcf,
        string? requestedCategory,
        out string? normalizedNcf,
        out string? normalizedCategory,
        out ActionResult? errorResult)
    {
        if (!TryResolveNcfData(
                requestedNcf,
                requestedCategory,
                out normalizedNcf,
                out normalizedCategory,
                out errorResult))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedNcf) &&
            IsDuplicateNcf(normalizedNcf, invoice.Id))
        {
            errorResult = DuplicateNcfConflict(normalizedNcf);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedNcf))
        {
            invoice.NcfNumber = normalizedNcf;
            invoice.NcfCategory = normalizedCategory ?? invoice.NcfCategory;
        }
        else if (string.IsNullOrWhiteSpace(invoice.NcfNumber))
        {
            var categoryForGeneration = normalizedCategory
                ?? invoice.NcfCategory
                ?? NcfCategoryCatalog.DefaultCategoryCode;

            invoice.NcfCategory = categoryForGeneration;
            invoice.NcfNumber = GenerateNcf(categoryForGeneration);
            normalizedNcf = invoice.NcfNumber;
        }
        else
        {
            normalizedNcf = invoice.NcfNumber;
            if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
                NcfCategoryCatalog.TryInferCategoryFromNcf(invoice.NcfNumber, out var existingDefinition) &&
                !string.Equals(normalizedCategory, existingDefinition.Code, StringComparison.OrdinalIgnoreCase))
            {
                errorResult = NcfCategoryMismatchBadRequest(existingDefinition.Code, normalizedCategory);
                return false;
            }

            normalizedCategory ??= invoice.NcfCategory;
        }

        if (string.IsNullOrWhiteSpace(invoice.NcfNumber))
        {
            errorResult = BadRequest(new ProblemDetails
            {
                Title = "NCF is required",
                Detail = "An NCF value could not be determined.",
            });
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedCategory) &&
            NcfCategoryCatalog.TryInferCategoryFromNcf(invoice.NcfNumber, out var inferredDefinition))
        {
            normalizedCategory = inferredDefinition.Code;
        }

        normalizedCategory ??= NcfCategoryCatalog.DefaultCategoryCode;
        invoice.NcfCategory ??= normalizedCategory;

        if (IsDuplicateNcf(invoice.NcfNumber!, invoice.Id))
        {
            errorResult = DuplicateNcfConflict(invoice.NcfNumber!);
            return false;
        }

        invoice.InvoiceGeneratedAt ??= DateTime.UtcNow;

        // Link invoice to its fiscal regime and bump the count (only on first assignment)
        if (!invoice.FiscalRegimeId.HasValue && !string.IsNullOrWhiteSpace(normalizedCategory))
        {
            var categoryCode = normalizedCategory; // copy out-param so it's usable inside the lambda
            var regime = _context.FiscalRegimes
                .FirstOrDefault(r => r.Code == categoryCode);

            if (regime is not null)
            {
                invoice.FiscalRegimeId = regime.Id;
                regime.InvoiceCount++;
            }
        }

        _context.SaveChanges();
        normalizedNcf = invoice.NcfNumber;
        errorResult = null;
        return true;
    }

    private bool IsDuplicateNcf(string normalizedNcf, Guid? excludingId) =>
        _context.Invoices.Any(invoice =>
            invoice.NcfNumber == normalizedNcf &&
            (!excludingId.HasValue || invoice.Id != excludingId.Value));

    private ActionResult DuplicateNcfConflict(string normalizedNcf) =>
        Conflict(new ProblemDetails
        {
            Title = "Duplicate NCF",
            Detail = $"The NCF \"{normalizedNcf}\" is already assigned to another invoice.",
        });

    private bool TryResolveNcfData(
        string? ncfNumber,
        string? categoryCode,
        out string? normalizedNcf,
        out string? normalizedCategory,
        out ActionResult? errorResult)
    {
        normalizedNcf = NormalizeNcfNumber(ncfNumber);
        if (!TryNormalizeCategoryCode(categoryCode, out normalizedCategory, out errorResult))
        {
            return false;
        }

        if (NcfCategoryCatalog.TryInferCategoryFromNcf(normalizedNcf, out var inferredDefinition))
        {
            if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
                !string.Equals(normalizedCategory, inferredDefinition.Code, StringComparison.OrdinalIgnoreCase))
            {
                errorResult = NcfCategoryMismatchBadRequest(inferredDefinition.Code, normalizedCategory);
                return false;
            }

            normalizedCategory = inferredDefinition.Code;
        }

        errorResult = null;
        return true;
    }

    private bool TryNormalizeCategoryCode(
        string? categoryCode,
        out string? normalizedCategory,
        out ActionResult? errorResult)
    {
        normalizedCategory = null;
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            errorResult = null;
            return true;
        }

        if (!NcfCategoryCatalog.TryGetByCode(categoryCode, out var definition))
        {
            errorResult = InvalidNcfCategoryBadRequest(categoryCode.Trim());
            return false;
        }

        normalizedCategory = definition.Code;
        errorResult = null;
        return true;
    }

    private ActionResult InvalidNcfCategoryBadRequest(string providedCategory) =>
        BadRequest(new ProblemDetails
        {
            Title = "Invalid NCF category",
            Detail = $"The NCF category \"{providedCategory}\" is not supported.",
        });

    private ActionResult NcfCategoryMismatchBadRequest(string inferredCategory, string providedCategory) =>
        BadRequest(new ProblemDetails
        {
            Title = "NCF category mismatch",
            Detail = $"The NCF value belongs to category \"{inferredCategory}\" but \"{providedCategory}\" was requested.",
        });

    private static string? NormalizeNcfNumber(string? ncfNumber)
    {
        if (string.IsNullOrWhiteSpace(ncfNumber))
        {
            return null;
        }

        var trimmed = ncfNumber.Trim().ToUpperInvariant();
        var compact = new string(trimmed.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)).ToArray());
        const string ncfPrefix = "NCF";

        if (compact.Length >= ncfPrefix.Length &&
            compact.StartsWith(ncfPrefix, StringComparison.Ordinal))
        {
            var suffix = compact[ncfPrefix.Length..];
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"{ncfPrefix}-{suffix}";
        }

        var normalizedKnown = NcfCategoryCatalog.TryNormalizeKnownNcfNumber(compact);
        if (!string.IsNullOrWhiteSpace(normalizedKnown))
        {
            return normalizedKnown;
        }

        return trimmed;
    }

    private bool TryResolveCustomerRecord(
        string? customerName,
        string? customerAddress,
        string? customerContact,
        out Customer customer,
        out ActionResult? errorResult)
    {
        customer = default!;
        var normalizedName = customerName?.Trim() ?? string.Empty;
        var normalizedAddress = customerAddress?.Trim() ?? string.Empty;
        var normalizedContact = customerContact?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedAddress) ||
            string.IsNullOrWhiteSpace(normalizedContact))
        {
            errorResult = BadRequest(new ProblemDetails
            {
                Title = "Customer details required",
                Detail = "Customer name, address, and contact are required.",
            });
            return false;
        }

        customer = _context.Customers
            .FirstOrDefault(existing => existing.Name == normalizedName)
            ?? new Customer
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow,
            };

        customer.Address = normalizedAddress;
        customer.Contact = normalizedContact;
        customer.UpdatedAt = DateTime.UtcNow;

        if (_context.Entry(customer).State == EntityState.Detached)
        {
            _context.Customers.Add(customer);
        }

        errorResult = null;
        return true;
    }
}

public record InvoiceBatchRequest(IReadOnlyList<Guid> Ids);
