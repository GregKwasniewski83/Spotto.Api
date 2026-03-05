using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces
{
    public interface IPaymentCacheService
    {
        Task CachePaymentStatusAsync(string paymentId, PaymentDto payment);
        Task<PaymentDto?> GetCachedPaymentStatusAsync(string paymentId);
        Task InvalidatePaymentCacheAsync(string paymentId);
        Task<bool> IsPaymentCachedAsync(string paymentId);
    }
}