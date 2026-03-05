using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services
{
    public class PaymentCacheService : IPaymentCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<PaymentCacheService> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);

        public PaymentCacheService(IMemoryCache cache, ILogger<PaymentCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task CachePaymentStatusAsync(string paymentId, PaymentDto payment)
        {
            try
            {
                var cacheKey = GetCacheKey(paymentId);
                var cacheValue = new PaymentCacheEntry
                {
                    Payment = payment,
                    CachedAt = DateTime.UtcNow,
                    IsCompleted = payment.Status == "COMPLETED" || payment.Status == "FAILED"
                };

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheExpiration,
                    Priority = CacheItemPriority.High,
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                };

                _cache.Set(cacheKey, cacheValue, cacheOptions);
                
                _logger.LogInformation("Cached payment status for payment {PaymentId} with status {Status}", 
                    paymentId, payment.Status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache payment status for payment {PaymentId}", paymentId);
            }
        }

        public async Task<PaymentDto?> GetCachedPaymentStatusAsync(string paymentId)
        {
            try
            {
                var cacheKey = GetCacheKey(paymentId);
                
                if (_cache.TryGetValue(cacheKey, out PaymentCacheEntry cacheEntry))
                {
                    _logger.LogDebug("Cache hit for payment {PaymentId}", paymentId);
                    return cacheEntry.Payment;
                }

                _logger.LogDebug("Cache miss for payment {PaymentId}", paymentId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cached payment status for payment {PaymentId}", paymentId);
                return null;
            }
        }

        public async Task InvalidatePaymentCacheAsync(string paymentId)
        {
            try
            {
                var cacheKey = GetCacheKey(paymentId);
                _cache.Remove(cacheKey);
                
                _logger.LogInformation("Invalidated cache for payment {PaymentId}", paymentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for payment {PaymentId}", paymentId);
            }
        }

        public async Task<bool> IsPaymentCachedAsync(string paymentId)
        {
            var cacheKey = GetCacheKey(paymentId);
            return _cache.TryGetValue(cacheKey, out _);
        }

        private string GetCacheKey(string paymentId) => $"payment_status_{paymentId}";

        private class PaymentCacheEntry
        {
            public PaymentDto Payment { get; set; }
            public DateTime CachedAt { get; set; }
            public bool IsCompleted { get; set; }
        }
    }
}