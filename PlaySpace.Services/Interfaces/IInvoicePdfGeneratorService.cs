using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

/// <summary>
/// Service for generating invoice PDFs
/// </summary>
public interface IInvoicePdfGeneratorService
{
    /// <summary>
    /// Generates a PDF invoice document
    /// </summary>
    /// <param name="invoice">The KSeF invoice to generate PDF for</param>
    /// <returns>PDF document as byte array</returns>
    byte[] GenerateInvoicePdf(KSeFInvoice invoice);
}
