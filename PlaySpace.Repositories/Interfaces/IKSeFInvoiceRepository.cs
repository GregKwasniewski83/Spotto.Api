using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IKSeFInvoiceRepository
{
    KSeFInvoice CreateInvoice(KSeFInvoice invoice);
    KSeFInvoice? GetInvoiceById(Guid id);
    KSeFInvoice? GetInvoiceByPaymentId(Guid paymentId);
    KSeFInvoice? GetInvoiceByKSeFReference(string ksefReferenceNumber);
    List<KSeFInvoice> GetInvoicesByBusinessProfile(Guid businessProfileId);
    KSeFInvoice UpdateInvoice(KSeFInvoice invoice);
    string GenerateInvoiceNumber(string prefix, Guid businessProfileId);
}
