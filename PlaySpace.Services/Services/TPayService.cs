using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services
{
    public class TPayService : ITPayService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TPayService> _logger;
        private readonly TPayConfiguration _config;
        private string _accessToken;
        private DateTime _tokenExpiry;

        public TPayService(IHttpClientFactory httpClientFactory, ILogger<TPayService> logger, IOptions<TPayConfiguration> config)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config.Value;

            // Debug: Log configuration values (remove sensitive data in production)
            Console.WriteLine($"DEBUG - TPay Config: BaseUrl='{_config.BaseUrl}', ApiKey='{(!string.IsNullOrEmpty(_config.ApiKey) ? "***SET***" : "NULL/EMPTY")}', ApiPassword='{(!string.IsNullOrEmpty(_config.ApiPassword) ? "***SET***" : "NULL/EMPTY")}', MerchantId='{_config.MerchantId}', IsSandbox={_config.IsSandbox}");

            // Validate configuration
            if (string.IsNullOrEmpty(_config.BaseUrl))
                throw new ConfigurationException("TPay BaseUrl is not configured.");
            if (string.IsNullOrEmpty(_config.ApiKey))
                throw new ConfigurationException("TPay ApiKey is not configured.");
            if (string.IsNullOrEmpty(_config.ApiPassword))
                throw new ConfigurationException("TPay ApiPassword is not configured.");

            try
            {
                // Validate the base URL format
                var uri = new Uri(_config.BaseUrl);
            }
            catch (UriFormatException ex)
            {
                throw new ConfigurationException($"Invalid TPay BaseUrl format: {_config.BaseUrl}", ex);
            }
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _accessToken;
            }

            try
            {
                var formParams = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", _config.ApiKey),
                    new KeyValuePair<string, string>("client_secret", _config.ApiPassword)
                };

                var content = new FormUrlEncodedContent(formParams);

                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync($"{_config.BaseUrl}/oauth/auth", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayAuthenticationException(
                        $"Failed to authenticate with TPay. Status: {response.StatusCode}, Response: {responseJson}");
                }

                var authResponse = JsonSerializer.Deserialize<TPayAuthResponse>(responseJson);

                if (authResponse == null || string.IsNullOrEmpty(authResponse.access_token))
                {
                    throw new TPayAuthenticationException("Invalid authentication response from TPay.");
                }

                _accessToken = authResponse.access_token;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(authResponse.expires_in - 60); // Refresh 1 minute early

                return _accessToken;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay authentication service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay authentication request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay authentication response.", ex);
            }
        }

        public async Task<TPayTransactionResponse> CreateTransactionAsync(TPayTransactionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.amount <= 0)
                throw new ValidationException("Transaction amount must be greater than zero.");

            if (string.IsNullOrEmpty(request.description))
                throw new ValidationException("Transaction description is required.");

            try
            {
                var token = await GetAccessTokenAsync();
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.PostAsync($"{_config.BaseUrl}/transactions", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayTransactionException(
                        $"Failed to create transaction. Status: {response.StatusCode}, Response: {responseJson}", 
                        null);
                }

                var transactionResponse = JsonSerializer.Deserialize<TPayTransactionResponse>(responseJson);

                if (transactionResponse == null)
                {
                    throw new TPayTransactionException("Invalid transaction response from TPay.", null);
                }

                if (transactionResponse.result != "success")
                {
                    throw new TPayTransactionException(
                        $"TPay transaction failed: {transactionResponse.err}", 
                        transactionResponse.transactionId);
                }

                return transactionResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay transaction service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay transaction request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay transaction response.", ex);
            }
        }

        public async Task<bool> ValidateNotificationAsync(TPayNotification notification, string md5Key)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            if (string.IsNullOrEmpty(md5Key))
                throw new ConfigurationException("MD5 key for TPay notification validation is not configured.");

            try
            {
                var expectedHash = GenerateMd5Hash(notification, md5Key);
                return expectedHash.Equals(notification.md5sum, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                throw new TPayException("Failed to validate TPay notification.", ex);
            }
        }

        public async Task<TPayMarketplaceTransactionResponse> CreateMarketplaceTransactionAsync(TPayMarketplaceTransactionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.childTransactions == null || !request.childTransactions.Any())
                throw new ValidationException("At least one child transaction is required for marketplace transactions.");

            if (request.childTransactions.Any(ct => ct.amount <= 0))
                throw new ValidationException("All child transaction amounts must be greater than zero.");

            try
            {
                var token = await GetAccessTokenAsync();
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.PostAsync($"{_config.BaseUrl}/marketplace/v1/transaction", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayTransactionException(
                        $"Failed to create marketplace transaction. Status: {response.StatusCode}, Response: {responseJson}", 
                        null);
                }

                var transactionResponse = JsonSerializer.Deserialize<TPayMarketplaceTransactionResponse>(responseJson);

                if (transactionResponse == null)
                {
                    throw new TPayTransactionException("Invalid marketplace transaction response from TPay.", null);
                }

                return transactionResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay marketplace service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay marketplace transaction request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay marketplace transaction response.", ex);
            }
        }

        public async Task<TPayBusinessRegistrationResponse> RegisterBusinessAsync(TPayBusinessRegistrationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.email))
                throw new ValidationException("Business email is required.");

            if (string.IsNullOrEmpty(request.taxId))
                throw new ValidationException("Business tax ID is required.");

            try
            {
                var token = await GetAccessTokenAsync();
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.PostAsync($"{_config.BaseUrl}/accounts", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayTransactionException(
                        $"Failed to register business with TPay. Status: {response.StatusCode}, Response: {responseJson}", 
                        null);
                }

                var registrationResponse = JsonSerializer.Deserialize<TPayBusinessRegistrationResponse>(responseJson);

                if (registrationResponse == null)
                {
                    throw new TPayTransactionException("Invalid business registration response from TPay.", null);
                }

                if (registrationResponse.result != "success")
                {
                    throw new TPayTransactionException(
                        $"TPay business registration failed: {registrationResponse.result}", 
                        registrationResponse,
                        null);
                }

                return registrationResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay accounts service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay business registration request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay business registration response.", ex);
            }
        }

        public async Task<TPayDictionaryResponse<TPayLegalFormItem>> GetLegalFormsAsync()
        {
            try
            {
                var token = await GetAccessTokenAsync();
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync($"{_config.BaseUrl}/accounts/legalForm");
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayException($"Failed to fetch legal forms from TPay. Status: {response.StatusCode}, Response: {responseJson}");
                }

                var dictionaryResponse = JsonSerializer.Deserialize<TPayDictionaryResponse<TPayLegalFormItem>>(responseJson);

                if (dictionaryResponse == null || dictionaryResponse.result != "success")
                {
                    throw new TPayException("Invalid legal forms response from TPay.");
                }

                return dictionaryResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay legal forms service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay legal forms request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay legal forms response.", ex);
            }
        }

        public async Task<TPayDictionaryResponse<TPayCategoryItem>> GetCategoriesAsync()
        {
            try
            {
                var token = await GetAccessTokenAsync();
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync($"{_config.BaseUrl}/accounts/category");
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayException($"Failed to fetch categories from TPay. Status: {response.StatusCode}, Response: {responseJson}");
                }

                var dictionaryResponse = JsonSerializer.Deserialize<TPayDictionaryResponse<TPayCategoryItem>>(responseJson);

                if (dictionaryResponse == null || dictionaryResponse.result != "success")
                {
                    throw new TPayException("Invalid categories response from TPay.");
                }

                return dictionaryResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to communicate with TPay categories service.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay categories request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay categories response.", ex);
            }
        }

        public async Task<TPayPosResponse> GetPosInfoAsync()
        {
            try
            {
                var token = await GetAccessTokenAsync();
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync($"{_config.BaseUrl}/accounts/pos");
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new TPayException($"Failed to get POS info. Status: {response.StatusCode}, Response: {responseJson}");
                }

                var posResponse = JsonSerializer.Deserialize<TPayPosResponse>(responseJson);
                
                if (posResponse?.result != "success")
                {
                    throw new TPayException($"TPay POS request failed: {posResponse?.result}");
                }

                return posResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("TPay POS request failed due to network error.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay POS request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay POS response.", ex);
            }
        }

        public async Task<TPayRefundResponse> CreateRefundAsync(string transactionId, TPayRefundRequest request)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("Creating TPay refund for transaction {TransactionId}: {RefundRequest}", 
                    transactionId, json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.PostAsync($"{_config.BaseUrl}/marketplace/v1/transaction/{transactionId}/refund", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("TPay refund API response: Status={StatusCode}, Body={ResponseBody}", 
                    response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("TPay refund failed: Status={StatusCode}, Body={ResponseBody}", 
                        response.StatusCode, responseContent);
                    throw new TPayException($"TPay refund failed: {response.StatusCode} - {responseContent}");
                }

                var refundResponse = JsonSerializer.Deserialize<TPayRefundResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation("TPay refund created successfully for transaction {TransactionId}: {RefundId}", 
                    transactionId, refundResponse?.requestId);

                return refundResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new TPayException("Failed to create TPay refund due to network error.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TPayException("TPay refund request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new TPayException("Failed to parse TPay refund response.", ex);
            }
        }

        public string GenerateMd5Hash(TPayNotification notification, string securityCode)
        {
            var hashString = $"{notification.id}&{notification.tr_id}&{notification.tr_date}&{notification.tr_crc}&{notification.tr_amount}&{notification.tr_paid}&{notification.tr_desc}&{notification.tr_status}&{notification.tr_error}&{notification.tr_email}&{securityCode}";
            
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(hashString));
                return Convert.ToHexString(hash).ToLower();
            }
        }
    }
}