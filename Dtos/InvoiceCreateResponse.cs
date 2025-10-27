using IORManager.Models;

namespace IORManager.Dtos;

public record InvoiceCreateResponse(Invoice Invoice, string PdfFileName, string PdfBase64);
