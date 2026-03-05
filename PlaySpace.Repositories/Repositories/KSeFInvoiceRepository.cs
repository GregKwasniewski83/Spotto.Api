using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class KSeFInvoiceRepository : IKSeFInvoiceRepository
{
    private readonly PlaySpaceDbContext _context;

    public KSeFInvoiceRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public KSeFInvoice CreateInvoice(KSeFInvoice invoice)
    {
        _context.KSeFInvoices.Add(invoice);
        _context.SaveChanges();
        return invoice;
    }

    public KSeFInvoice? GetInvoiceById(Guid id)
    {
        return _context.KSeFInvoices
            .Include(i => i.Payment)
            .Include(i => i.Reservation)
            .Include(i => i.BusinessProfile)
            .Include(i => i.User)
            .FirstOrDefault(i => i.Id == id);
    }

    public KSeFInvoice? GetInvoiceByPaymentId(Guid paymentId)
    {
        return _context.KSeFInvoices
            .Include(i => i.Payment)
            .Include(i => i.Reservation)
            .Include(i => i.BusinessProfile)
            .Include(i => i.User)
            .FirstOrDefault(i => i.PaymentId == paymentId);
    }

    public KSeFInvoice? GetInvoiceByKSeFReference(string ksefReferenceNumber)
    {
        return _context.KSeFInvoices
            .FirstOrDefault(i => i.KSeFReferenceNumber == ksefReferenceNumber);
    }

    public List<KSeFInvoice> GetInvoicesByBusinessProfile(Guid businessProfileId)
    {
        return _context.KSeFInvoices
            .Include(i => i.Payment)
            .Include(i => i.Reservation)
            .Where(i => i.BusinessProfileId == businessProfileId)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public KSeFInvoice UpdateInvoice(KSeFInvoice invoice)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        _context.KSeFInvoices.Update(invoice);
        _context.SaveChanges();
        return invoice;
    }

    public string GenerateInvoiceNumber(string prefix, Guid businessProfileId)
    {
        var today = DateTime.UtcNow;
        var year = today.Year;
        var month = today.Month;

        // Get the count of invoices for this month for this specific business
        var monthlyCount = _context.KSeFInvoices
            .Count(i => i.BusinessProfileId == businessProfileId &&
                       i.IssueDate.Year == year &&
                       i.IssueDate.Month == month);

        // Format: FA/001/01/2026 (per business, resets monthly)
        return $"{prefix}/{(monthlyCount + 1):D3}/{month:D2}/{year}";
    }
}
