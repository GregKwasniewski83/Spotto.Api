using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces
{
    public interface IPaymentService
    {
        PaymentDto ProcessPayment(PaymentDto payment);
        Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto paymentDto);
        Task<PaymentDto> ProcessSplitPaymentAsync(CreateSplitPaymentDto splitPaymentDto);
        Task<MarketplaceTransactionResponseDto> ProcessMarketplaceTransactionAsync(CreateMarketplaceTransactionDto marketplaceDto);
        Task<PaymentDto> ProcessTrainingPaymentAsync(CreateTrainingPaymentDto trainingPaymentDto);
        Task<PaymentDto> HandleTPayNotificationAsync(TPayNotification notification);
        Task<PaymentDto> HandleTPayMarketplaceNotificationAsync(TPayMarketplaceNotification notification);
        Task<PaymentDto> GetPaymentByIdAsync(Guid paymentId);
        Task<PaymentDto> GetPaymentByTransactionIdAsync(string transactionId);
        Task<List<PaymentDto>> GetPendingPaymentsForUserAsync(Guid userId);
        Task<PaymentDto> CancelPaymentAsync(Guid paymentId, Guid userId);
        Task<PaymentDto> RefundPaymentAsync(Guid paymentId, decimal refundAmount, Guid facilityId);
        Task ProcessCompletedPaymentAsync(Guid paymentId);
    }
}
