using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface IKSeFInvoiceService
{
    /// <summary>
    /// Creates and sends an invoice to KSeF after successful payment
    /// </summary>
    Task<KSeFInvoice> CreateInvoiceFromPaymentAsync(Guid paymentId);

    /// <summary>
    /// Gets invoice by ID
    /// </summary>
    KSeFInvoice? GetInvoice(Guid invoiceId);

    /// <summary>
    /// Gets invoice by payment ID
    /// </summary>
    KSeFInvoice? GetInvoiceByPayment(Guid paymentId);

    /// <summary>
    /// Gets all invoices for a business profile
    /// </summary>
    List<KSeFInvoice> GetInvoicesByBusinessProfile(Guid businessProfileId);

    /// <summary>
    /// Checks invoice status in KSeF and updates local record
    /// </summary>
    Task<KSeFInvoice> CheckInvoiceStatusAsync(Guid invoiceId);
}
