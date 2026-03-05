namespace PlaySpace.Domain.Models
{
    public class TPayConfiguration
    {
        public string MerchantId { get; set; }
        public string ApiKey { get; set; }
        public string ApiPassword { get; set; }
        public string BaseUrl { get; set; }
        public string NotificationUrl { get; set; }
        public string ReturnUrl { get; set; }
        public string Md5Key { get; set; }
        public bool IsSandbox { get; set; }
        public string OfferCode { get; set; } = "NC6Pr";
    }
}