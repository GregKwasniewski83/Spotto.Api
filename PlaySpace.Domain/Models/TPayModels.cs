namespace PlaySpace.Domain.Models
{
    public class TPayTransactionRequest
    {
        public decimal amount { get; set; }
        public string description { get; set; }
        public string hiddenDescription { get; set; }
        public Payer payer { get; set; }
        public PaymentCallbacks callbacks { get; set; }
    }

    public class Payer
    {
        public string email { get; set; }
        public string name { get; set; }
        public string phone { get; set; }
        public Address address { get; set; }
    }

    public class Address
    {
        public string street { get; set; }
        public string city { get; set; }
        public string code { get; set; }
        public string country { get; set; }
    }

    public class PaymentCallbacks
    {
        public PayerUrls payerUrls { get; set; }
        public NotificationCallback notification { get; set; }
    }

    public class PayerUrls
    {
        public string success { get; set; }
        public string error { get; set; }
    }

    public class NotificationCallback
    {
        public string url { get; set; }
        public string email { get; set; }
    }

    public class TPayTransactionResponse
    {
        public string result { get; set; }
        public string title { get; set; }
        public string transactionId { get; set; }
        public string transactionPaymentUrl { get; set; }
        public string err { get; set; }
    }

    public class TPayNotification
    {
        // Fields from actual TPay webhook format
        public string? status { get; set; }
        public string? error { get; set; }
        public decimal? amount { get; set; }
        public string? description { get; set; }
        public string? crc { get; set; }
        public string? email { get; set; }
        public string? test { get; set; }
        public string? ssl { get; set; }
        public string? url { get; set; }
        public string? csrf { get; set; }
        
        // Legacy fields (for compatibility)
        public string? id { get; set; }
        public string? tr_id { get; set; }
        public string? tr_date { get; set; }
        public string? tr_crc { get; set; }
        public decimal? tr_amount { get; set; }
        public decimal? tr_paid { get; set; }
        public string? tr_desc { get; set; }
        public string? tr_status { get; set; }
        public string? tr_error { get; set; }
        public string? tr_email { get; set; }
        public string? md5sum { get; set; }
        public bool? test_mode { get; set; }
        public string? wallet { get; set; }
    }

    public class TPayMarketplaceNotification
    {
        public string type { get; set; }
        public TPayMarketplaceNotificationData data { get; set; }
    }

    public class TPayMarketplaceNotificationData
    {
        public string transactionId { get; set; }
        public string transactionTitle { get; set; }
        public decimal transactionAmount { get; set; }
        public decimal transactionPaidAmount { get; set; }
        public string transactionStatus { get; set; }
        public string transactionHiddenDescription { get; set; }
        public string payerEmail { get; set; }
        public string transactionDate { get; set; }
        public string transactionDescription { get; set; }
        public string? cardToken { get; set; }
    }

    public class TPayMarketplaceNotificationResponse
    {
        public bool result { get; set; } = true;
    }

    public class TPayAuthRequest
    {
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }

    public class TPayAuthResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string scope { get; set; }
    }

    // Marketplace transaction models for split payments
    public class TPayMarketplaceTransactionRequest
    {
        public string currency { get; set; } = "PLN";
        public string description { get; set; }
        public string hiddenDescription { get; set; }
        public string languageCode { get; set; } = "PL";
        public string preSelectedChannelId { get; set; }
        public MarketplacePos pos { get; set; }
        public MarketplaceBillingAddress billingAddress { get; set; }
        public List<TransactionCallback> transactionCallbacks { get; set; } = new();
        public List<ChildTransaction> childTransactions { get; set; } = new();
    }

    public class MarketplacePos
    {
        public string id { get; set; }
    }


    public class MarketplaceBillingAddress
    {
        public string email { get; set; }
        public string name { get; set; }
        public string phone { get; set; }
        public string street { get; set; }
        public string postalCode { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string houseNo { get; set; }
        public string flatNo { get; set; }
    }

    public class ChildTransaction
    {
        public decimal amount { get; set; }
        public string description { get; set; }
        public string hiddenDescription { get; set; }
        public MarketplaceMerchant merchant { get; set; }
        public List<MarketplaceProduct> products { get; set; } = new();
    }

    public class MarketplaceMerchant
    {
        public string id { get; set; }
    }

    public class MarketplaceProduct
    {
        public string name { get; set; }
        public string externalId { get; set; }
        public decimal quantity { get; set; }
        public decimal unitPrice { get; set; }
    }

    public class TransactionCallback
    {
        public int type { get; set; }
        public string value { get; set; }
    }

    public class TPayMarketplaceTransactionResponse
    {
        public string transactionId { get; set; }
        public string title { get; set; }
        public string posId { get; set; }
        public string status { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public string hiddenDescription { get; set; }
        public MarketplaceBillingAddress billingAddress { get; set; }
        public List<ChildTransactionResponse> childTransactions { get; set; } = new();
        public string paymentUrl { get; set; }
    }

    public class ChildTransactionResponse
    {
        public string transactionId { get; set; }
        public decimal amount { get; set; }
        public string description { get; set; }
        public string hiddenDescription { get; set; }
        public MarketplaceMerchant merchant { get; set; }
        public List<MarketplaceProduct> products { get; set; } = new();
    }

    // TPay Business Registration Models
    public class TPayBusinessRegistrationRequest
    {
        public string offerCode { get; set; }
        public string email { get; set; }
        public BusinessPhone phone { get; set; }
        public string taxId { get; set; }
        public string regon { get; set; }
        public string krs { get; set; }
        public int legalForm { get; set; }
        public int categoryId { get; set; }
        public string mcc { get; set; }
        public bool merchantApiConsent { get; set; }
        public List<BusinessWebsite> website { get; set; } = new();
        public List<BusinessAddress> address { get; set; } = new();
        public List<BusinessPerson> person { get; set; } = new();
    }

    public class BusinessPhone
    {
        public string phoneNumber { get; set; }
        public string phoneCountry { get; set; }
    }

    public class BusinessWebsite
    {
        public string name { get; set; }
        public string friendlyName { get; set; }
        public string description { get; set; }
        public string url { get; set; }
    }

    public class BusinessAddress
    {
        public string friendlyName { get; set; }
        public string name { get; set; }
        public string street { get; set; }
        public string houseNumber { get; set; }
        public string roomNumber { get; set; }
        public string postalCode { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string phone { get; set; }
        public bool isMain { get; set; }
        public bool isCorrespondence { get; set; }
        public bool isInvoice { get; set; }
    }

    public class BusinessPerson
    {
        public string name { get; set; }
        public string surname { get; set; }
        public bool isRepresentative { get; set; }
        public bool isContactPerson { get; set; }
        public List<PersonContact> contact { get; set; } = new();
    }

    public class PersonContact
    {
        public int type { get; set; }
        public string contact { get; set; }
    }

    public class TPayBusinessRegistrationResponse
    {
        public string result { get; set; }
        public string requestId { get; set; }
        public string id { get; set; }
        public string offerCode { get; set; }
        public string email { get; set; }
        public string taxId { get; set; }
        public string regon { get; set; }
        public string krs { get; set; }
        public int legalForm { get; set; }
        public int categoryId { get; set; }
        public int verificationStatus { get; set; }
        public string activationLink { get; set; }
        public List<RegisteredWebsite> website { get; set; } = new();
        public List<RegisteredAddress> address { get; set; } = new();
        public List<object> person { get; set; } = new();
        public List<TPayError> errors { get; set; } = new();
    }

    public class TPayError
    {
        public string errorCode { get; set; } = string.Empty;
        public string errorMessage { get; set; } = string.Empty;
        public string fieldName { get; set; } = string.Empty;
        public string devMessage { get; set; } = string.Empty;
        public string docUrl { get; set; } = string.Empty;
    }

    public class RegisteredWebsite
    {
        public string posId { get; set; }
        public string accountId { get; set; }
        public string name { get; set; }
        public string friendlyName { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public WebsiteDate date { get; set; }
        public WebsiteSettings settings { get; set; }
        public int verificationScope { get; set; }
        public string verificationDate { get; set; }
        public int verificationStatus { get; set; }
    }

    public class WebsiteDate
    {
        public string create { get; set; }
        public string modification { get; set; }
    }

    public class WebsiteSettings
    {
        public string confirmationCode { get; set; }
        public bool isTestMode { get; set; }
    }

    public class RegisteredAddress
    {
        public string addressId { get; set; }
        public string friendlyName { get; set; }
        public string name { get; set; }
        public string street { get; set; }
        public string houseNumber { get; set; }
        public string roomNumber { get; set; }
        public string postalCode { get; set; }
        public string postOffice { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string phone { get; set; }
        public bool isMain { get; set; }
        public bool isCorrespondence { get; set; }
        public bool isInvoice { get; set; }
    }

    // TPay Dictionary Models
    public class TPayDictionaryResponse<T>
    {
        public string result { get; set; }
        public string requestId { get; set; }
        public List<T> list { get; set; } = new();
    }

    public class TPayLegalFormItem
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class TPayCategoryItem
    {
        public int id { get; set; }
        public string name { get; set; }
        public int? parentId { get; set; }
    }

    public class TPayPosResponse
    {
        public string result { get; set; }
        public string requestId { get; set; }
        public List<TPayPosItem> list { get; set; } = new();
    }

    public class TPayPosItem
    {
        public string posId { get; set; }
        public string accountId { get; set; }
        public string name { get; set; }
        public string friendlyName { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public bool enabled { get; set; }
        public TPayPosDate date { get; set; }
        public TPayPosSettings settings { get; set; }
    }

    public class TPayPosDate
    {
        public string create { get; set; }
        public string modification { get; set; }
    }

    public class TPayPosSettings
    {
        public string confirmationCode { get; set; }
        public bool testMode { get; set; }
    }

    // TPay Refund Models
    public class TPayRefundRequest
    {
        public List<RefundChildTransaction> childTransactions { get; set; } = new();
    }

    public class RefundChildTransaction
    {
        public string id { get; set; }
        public string merchantId { get; set; }
        public decimal amount { get; set; }
        public List<RefundProduct> products { get; set; } = new();
    }

    public class RefundProduct
    {
        public string name { get; set; }
        public string externalId { get; set; }
        public decimal quantity { get; set; }
        public decimal unitPrice { get; set; }
    }

    public class TPayRefundResponse
    {
        public string result { get; set; }
        public string requestId { get; set; }
        public string transactionId { get; set; }
        public List<RefundChildTransactionResponse> childTransactions { get; set; } = new();
    }

    public class RefundChildTransactionResponse
    {
        public string transactionId { get; set; }
        public string status { get; set; }
        public decimal amount { get; set; }
        public string description { get; set; }
        public MarketplaceMerchant merchant { get; set; }
        public List<RefundProduct> products { get; set; } = new();
    }
}