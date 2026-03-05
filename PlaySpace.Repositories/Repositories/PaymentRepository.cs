using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly PlaySpaceDbContext _context;

        public PaymentRepository(PlaySpaceDbContext context)
        {
            _context = context;
        }

        public Payment ProcessPayment(PaymentDto payment)
        {
            throw new NotImplementedException("Use async methods instead");
        }

        public async Task<Payment> CreatePaymentAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> UpdatePaymentAsync(Payment payment)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> GetPaymentByCrcAsync(string crc)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.Id.ToString() == crc);
        }

        public async Task<Payment> GetPaymentByIdAsync(Guid id)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Payment> GetByIdAsync(Guid id)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Payment> UpdateAsync(Payment payment)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> GetPaymentByTransactionIdAsync(string transactionId)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.TPayTransactionId == transactionId);
        }

        public async Task<List<Payment>> GetPendingPaymentsForUserAsync(Guid userId, DateTime since)
        {
            return await _context.Payments
                .Where(p => p.UserId == userId && 
                           p.Status == "PENDING" && 
                           p.CreatedAt >= since)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}
