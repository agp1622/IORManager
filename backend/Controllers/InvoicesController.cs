using System.Linq;
using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
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
    private readonly IORManagerContext _context;

    public InvoicesController(
        IFinancialDocumentRepository<Invoice> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator,
        INcfNumberGenerator ncfNumberGenerator,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
        _ncfNumberGenerator = ncfNumberGenerator;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Invoice>> GetInvoices()
    {
        var invoices = _repository.GetAll().ToList();
        PopulateQuoteNumbers(invoices);
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
        return Ok(invoice);
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
            var nextNcf = _ncfNumberGenerator.PeekNextNumber(regime.Code);

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
        var nextNumber = _ncfNumberGenerator.PeekNextNumber(normalizedCategory);
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

    [HttpPost("{id:guid}/ncf")]
    public ActionResult<NcfAssignmentResponse> AssignNcf(Guid id, [FromBody] NcfAssignmentRequest? request)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

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
            invoice.NcfNumber = _ncfNumberGenerator.GenerateNextNumber(categoryForGeneration);
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
