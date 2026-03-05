using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Payment ProcessPayment(PaymentDto payment);
        Task<Payment> CreatePaymentAsync(Payment payment);
        Task<Payment> UpdatePaymentAsync(Payment payment);
        Task<Payment> GetPaymentByCrcAsync(string crc);
        Task<Payment> GetPaymentByIdAsync(Guid id);
        Task<Payment> GetByIdAsync(Guid id);
        Task<Payment> UpdateAsync(Payment payment);
        Task<Payment> GetPaymentByTransactionIdAsync(string transactionId);
        Task<List<Payment>> GetPendingPaymentsForUserAsync(Guid userId, DateTime since);
    }
}
