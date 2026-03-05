using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task<bool> SendPaymentCompletedAsync(string pushToken, Guid paymentId, Guid? reservationId = null);
        Task<bool> SendPaymentFailedAsync(string pushToken, Guid paymentId, string reason);
        Task<bool> SendReservationConfirmedAsync(string pushToken, Guid reservationId);
        Task<bool> SendReservationCancelledAsync(string pushToken, Guid reservationId, bool isRefund);
        Task<bool> SendCustomNotificationAsync(string pushToken, string title, string body, Dictionary<string, string>? data = null);
    }
    
    public class PushNotificationData
    {
        public string Type { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string ReservationId { get; set; } = string.Empty;
        public string DeepLink { get; set; } = string.Empty;
        public Dictionary<string, string> AdditionalData { get; set; } = new();
    }
}