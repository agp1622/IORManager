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
public class QuotesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Quote> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly INcfNumberGenerator _ncfNumberGenerator;
    private readonly IORManagerContext _context;

    public QuotesController(
        IFinancialDocumentRepository<Quote> repository,
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
    public ActionResult<IReadOnlyCollection<Quote>> GetQuotes()
        => Ok(_repository.GetAll());

    [HttpGet("{id:guid}")]
    public ActionResult<Quote> GetQuote(Guid id)
    {
        var quote = _repository.GetById(id);
        return quote is not null ? Ok(quote) : NotFound();
    }

    [HttpPost]
    public ActionResult<QuoteCreateResponse> CreateQuote([FromBody] InvoiceCreateRequest request)
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

        var quote = request.ToQuote();
        quote.Date = DateOnly.FromDateTime(DateTime.UtcNow);
        quote.Number = _numberGenerator.GenerateNextNumber();
        quote.CustomerId = customer.Id;
        quote.CustomerName = customer.Name;
        quote.CustomerAddress = customer.Address;
        quote.CustomerContact = customer.Contact;

        var savedQuote = _repository.Add(quote);
        var pdfBytes = _pdfService.GenerateQuotePdf(ToQuotePdfModel(savedQuote));
        var response = new QuoteCreateResponse(
            savedQuote,
            $"Quote-{savedQuote.Number}.pdf",
            Convert.ToBase64String(pdfBytes));

        return CreatedAtAction(nameof(GetQuote), new { id = savedQuote.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<Quote> UpdateQuote(Guid id, [FromBody] InvoiceUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var quote = _context.Quotes
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (quote is null)
        {
            return NotFound();
        }

        quote.CustomerName = request.CustomerName;
        quote.CustomerAddress = request.CustomerAddress.Trim();
        quote.CustomerContact = request.CustomerContact.Trim();
        quote.CurrencyCode = request.CurrencyCode;
        quote.CultureName = request.Locale;
        quote.ItbisRate = request.ItbisRate;

        if (!TryResolveCustomerRecord(
                quote.CustomerName,
                quote.CustomerAddress,
                quote.CustomerContact,
                out var customer,
                out var customerError))
        {
            return customerError!;
        }

        quote.CustomerId = customer.Id;
        quote.CustomerName = customer.Name;
        quote.CustomerAddress = customer.Address;
        quote.CustomerContact = customer.Contact;

        var existingLines = quote.Lines.ToList();
        if (existingLines.Count > 0)
        {
            _context.DocumentLines.RemoveRange(existingLines);
        }
        quote.Lines.Clear();

        foreach (var lineRequest in request.Lines)
        {
            quote.Lines.Add(lineRequest.ToDocumentLine());
        }

        quote.RecalculateTotal();
        _context.SaveChanges();

        return Ok(quote);
    }

    [HttpPost("{id:guid}/duplicate")]
    public ActionResult<Quote> DuplicateQuote(Guid id)
    {
        var source = GetQuoteWithLines(id);
        if (source is null)
        {
            return NotFound();
        }

        var duplicated = new Quote
        {
            Id = Guid.NewGuid(),
            Number = _numberGenerator.GenerateNextNumber(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            CustomerId = source.CustomerId,
            CustomerName = source.CustomerName,
            CustomerAddress = source.CustomerAddress,
            CustomerContact = source.CustomerContact,
            CurrencyCode = source.CurrencyCode,
            CultureName = source.CultureName,
            ItbisRate = source.ItbisRate,
            ConvertedInvoiceId = null,
            ConvertedAt = null,
            Lines = source.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };

        duplicated.RecalculateTotal();
        _context.Quotes.Add(duplicated);
        _context.SaveChanges();

        return CreatedAtAction(nameof(GetQuote), new { id = duplicated.Id }, duplicated);
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetQuotePdf(Guid id)
    {
        var quote = _repository.GetById(id);
        if (quote is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateQuotePdf(ToQuotePdfModel(quote));
        return File(pdfBytes, "application/pdf", $"Quote-{quote.Number}.pdf");
    }

    [HttpPost("{id:guid}/convert")]
    public ActionResult<QuoteConversionResponse> ConvertQuoteToInvoice(Guid id, [FromBody] NcfAssignmentRequest? request)
    {
        var quote = GetQuoteWithLines(id);
        if (quote is null)
        {
            return NotFound();
        }

        if (quote.ConvertedInvoiceId.HasValue)
        {
            var existingInvoice = _context.Invoices
                .AsNoTracking()
                .FirstOrDefault(invoice => invoice.Id == quote.ConvertedInvoiceId.Value);
            if (existingInvoice is not null)
            {
                return Ok(new QuoteConversionResponse(
                    existingInvoice.Id,
                    existingInvoice.Number,
                    existingInvoice.NcfNumber ?? string.Empty,
                    existingInvoice.NcfCategory ?? NcfCategoryCatalog.DefaultCategoryCode));
            }
        }

        if (!TryResolveNcfData(
                request?.NcfNumber,
                request?.NcfCategory,
                out var normalizedNcf,
                out var normalizedCategory,
                out var ncfError))
        {
            return ncfError!;
        }

        if (!string.IsNullOrWhiteSpace(normalizedNcf) && IsDuplicateNcf(normalizedNcf))
        {
            return DuplicateNcfConflict(normalizedNcf);
        }

        string? categoryForGeneration = null;
        if (request?.SkipNcf != true)
        {
            categoryForGeneration = normalizedCategory ?? NcfCategoryCatalog.DefaultCategoryCode;
            if (string.IsNullOrWhiteSpace(normalizedNcf))
            {
                normalizedNcf = _ncfNumberGenerator.GenerateNextNumber(categoryForGeneration);
            }

            if (IsDuplicateNcf(normalizedNcf))
            {
                return DuplicateNcfConflict(normalizedNcf);
            }
        }

        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber());
        while (_context.Invoices.Any(invoice => invoice.Number == invoiceNumber))
        {
            invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber());
        }

        var generatedAt = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = invoiceNumber,
            Date = DateOnly.FromDateTime(generatedAt),
            CurrencyCode = quote.CurrencyCode,
            CultureName = quote.CultureName,
            CustomerId = quote.CustomerId,
            CustomerName = quote.CustomerName,
            CustomerAddress = quote.CustomerAddress,
            CustomerContact = quote.CustomerContact,
            QuoteId = quote.Id,
            ItbisRate = quote.ItbisRate,
            NcfNumber = normalizedNcf,
            NcfCategory = normalizedCategory ?? categoryForGeneration ?? null,
            InvoiceGeneratedAt = generatedAt,
            Lines = quote.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };
        invoice.RecalculateTotal();

        quote.ConvertedInvoiceId = invoice.Id;
        quote.ConvertedAt = generatedAt;

        _context.Invoices.Add(invoice);
        _context.SaveChanges();

        return Ok(new QuoteConversionResponse(
            invoice.Id,
            invoice.Number,
            invoice.NcfNumber ?? string.Empty,
            invoice.NcfCategory ?? NcfCategoryCatalog.DefaultCategoryCode));
    }

    [HttpPost("{id:guid}/undo-conversion")]
    public ActionResult<Quote> UndoQuoteConversion(Guid id)
    {
        var quote = GetQuoteWithLines(id);
        if (quote is null)
        {
            return NotFound();
        }

        if (!quote.ConvertedInvoiceId.HasValue)
        {
            return Ok(quote);
        }

        var invoice = _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == quote.ConvertedInvoiceId.Value);

        if (invoice is not null)
        {
            var invoiceLines = invoice.Lines.ToList();
            if (invoiceLines.Count > 0)
            {
                _context.DocumentLines.RemoveRange(invoiceLines);
            }

            _context.Invoices.Remove(invoice);
        }

        quote.ConvertedInvoiceId = null;
        quote.ConvertedAt = null;
        _context.SaveChanges();

        return Ok(quote);
    }

    private Quote? GetQuoteWithLines(Guid id) =>
        _context.Quotes
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

    private bool IsDuplicateNcf(string normalizedNcf) =>
        _context.Invoices.Any(invoice => invoice.NcfNumber == normalizedNcf);

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
            errorResult = BadRequest(new ProblemDetails
            {
                Title = "Invalid NCF category",
                Detail = $"The NCF category \"{categoryCode.Trim()}\" is not supported.",
            });
            return false;
        }

        normalizedCategory = definition.Code;
        errorResult = null;
        return true;
    }

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

    private static Invoice ToQuotePdfModel(Quote quote)
    {
        var pdfModel = new Invoice
        {
            Id = quote.Id,
            Number = quote.Number,
            Date = quote.Date,
            CurrencyCode = quote.CurrencyCode,
            CultureName = quote.CultureName,
            CustomerName = quote.CustomerName,
            CustomerAddress = quote.CustomerAddress,
            CustomerContact = quote.CustomerContact,
            ItbisRate = quote.ItbisRate,
            Lines = quote.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };

        pdfModel.RecalculateTotal();
        return pdfModel;
    }
}

public record QuoteCreateResponse(Quote Quote, string PdfFileName, string PdfBase64);
