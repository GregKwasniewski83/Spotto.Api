using PlaySpace.Domain.Configuration;
using PlaySpace.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PlaySpace.Services.Implementation
{
    public class ExpoPushNotificationService : IPushNotificationService
    {
        private readonly ILogger<ExpoPushNotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _expoApiUrl = "https://exp.host/--/api/v2/push/send";
        private readonly string _deepLinkScheme;

        public ExpoPushNotificationService(ILogger<ExpoPushNotificationService> logger, IHttpClientFactory httpClientFactory, IOptions<FrontendConfiguration> frontendConfig)
        {
            _logger = logger;
            _deepLinkScheme = frontendConfig.Value.DeepLinkScheme;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-encoding", "gzip, deflate");
        }

        private class ExpoMessage
        {
            public string to { get; set; } = string.Empty;
            public string title { get; set; } = string.Empty;
            public string body { get; set; } = string.Empty;
            public Dictionary<string, object>? data { get; set; }
            public string? sound { get; set; } = "default";
            public int? badge { get; set; }
            public string? priority { get; set; } = "high";
        }

        private class ExpoResponseData
        {
            public string? status { get; set; }
            public string? id { get; set; }
            public string? message { get; set; }
            public Dictionary<string, object>? details { get; set; }
        }

        private class ExpoErrorResponse
        {
            public ExpoResponseData? data { get; set; }
        }

        private bool IsValidExpoToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // Check for Expo push token format: ExponentPushToken[...]
            return token.StartsWith("ExponentPushToken[") && token.EndsWith("]") && token.Length > 20;
        }

        private async Task<bool> SendExpoNotificationAsync(string pushToken, ExpoMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_expoApiUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Expo API response: {Response}", responseContent);
                    
                    try
                    {
                        // Try to parse as success response first (array format)
                        if (responseContent.TrimStart().StartsWith("["))
                        {
                            var expoResponses = JsonSerializer.Deserialize<ExpoResponseData[]>(responseContent, new JsonSerializerOptions 
                            { 
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                            });

                            if (expoResponses?.Length > 0)
                            {
                                var result = expoResponses[0];
                                if (result.status == "ok")
                                {
                                    _logger.LogInformation("Expo notification sent successfully. ID: {MessageId}", result.id);
                                    return true;
                                }
                                else
                                {
                                    _logger.LogWarning("Expo notification failed. Status: {Status}, Message: {Message}", result.status, result.message);
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            // Parse as error response (object format)
                            var errorResponse = JsonSerializer.Deserialize<ExpoErrorResponse>(responseContent, new JsonSerializerOptions 
                            { 
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                            });

                            if (errorResponse?.data != null)
                            {
                                if (errorResponse.data.status == "ok")
                                {
                                    _logger.LogInformation("Expo notification sent successfully. ID: {MessageId}", errorResponse.data.id);
                                    return true;
                                }
                                else
                                {
                                    _logger.LogError("Expo notification failed. Status: {Status}, Message: {Message}", 
                                        errorResponse.data.status, errorResponse.data.message);
                                    return false;
                                }
                            }
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Failed to deserialize Expo response. Raw response: {Response}", responseContent);
                        return false;
                    }
                }

                _logger.LogError("Failed to send Expo notification. Response: {Response}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Expo notification to token {Token}", pushToken);
                return false;
            }
        }

        public async Task<bool> SendPaymentCompletedAsync(string pushToken, Guid paymentId, Guid? reservationId = null)
        {
            if (string.IsNullOrWhiteSpace(pushToken) || !IsValidExpoToken(pushToken))
            {
                _logger.LogWarning("Invalid Expo token provided for payment {PaymentId}. Skipping notification.", paymentId);
                return false;
            }

            var data = new Dictionary<string, object>
            {
                ["type"] = "PAYMENT_COMPLETED",
                ["paymentId"] = paymentId.ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            };

            string deepLink;
            string body;

            if (reservationId.HasValue)
            {
                data["reservationId"] = reservationId.Value.ToString();
                deepLink = $"{_deepLinkScheme}://reservation/{reservationId.Value}";
                body = "Płatność udana! Rezerwacja utworzona.";
            }
            else
            {
                deepLink = $"{_deepLinkScheme}://payment/{paymentId}";
                body = "Płatność udana! Rezerwacja utworzona.";
            }

            data["deepLink"] = deepLink;

            var message = new ExpoMessage
            {
                to = pushToken,
                title = "Płatność udana! 💳",
                body = body,
                data = data,
                sound = "default",
                badge = 1,
                priority = "high"
            };

            return await SendExpoNotificationAsync(pushToken, message);
        }

        public async Task<bool> SendPaymentFailedAsync(string pushToken, Guid paymentId, string reason)
        {
            if (string.IsNullOrWhiteSpace(pushToken) || !IsValidExpoToken(pushToken))
            {
                _logger.LogWarning("Invalid Expo token provided for payment {PaymentId}. Skipping notification.", paymentId);
                return false;
            }

            var data = new Dictionary<string, object>
            {
                ["type"] = "PAYMENT_FAILED",
                ["paymentId"] = paymentId.ToString(),
                ["reason"] = reason,
                ["deepLink"] = $"{_deepLinkScheme}://payment/{paymentId}",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            };

            var message = new ExpoMessage
            {
                to = pushToken,
                title = "Płatność nieudana",
                body = "Wystąpił problem z płatnością. Spróbuj ponownie.",
                data = data,
                sound = "default",
                priority = "high"
            };

            return await SendExpoNotificationAsync(pushToken, message);
        }

        public async Task<bool> SendReservationConfirmedAsync(string pushToken, Guid reservationId)
        {
            if (string.IsNullOrWhiteSpace(pushToken) || !IsValidExpoToken(pushToken))
            {
                _logger.LogWarning("Invalid Expo token provided for reservation {ReservationId}. Skipping notification.", reservationId);
                return false;
            }

            var data = new Dictionary<string, object>
            {
                ["type"] = "RESERVATION_CONFIRMED",
                ["reservationId"] = reservationId.ToString(),
                ["deepLink"] = $"{_deepLinkScheme}://reservation/{reservationId}",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            };

            var message = new ExpoMessage
            {
                to = pushToken,
                title = "Rezerwacja potwierdzona! 🎉",
                body = "Twoja rezerwacja obiektu sportowego została potwierdzona.",
                data = data,
                sound = "default",
                badge = 1
            };

            return await SendExpoNotificationAsync(pushToken, message);
        }

        public async Task<bool> SendReservationCancelledAsync(string pushToken, Guid reservationId, bool isRefund)
        {
            if (string.IsNullOrWhiteSpace(pushToken) || !IsValidExpoToken(pushToken))
            {
                _logger.LogWarning("Invalid Expo token provided for reservation {ReservationId}. Skipping notification.", reservationId);
                return false;
            }

            var body = isRefund 
                ? "Twoja rezerwacja została anulowana i zwrot jest przetwarzany."
                : "Twoja rezerwacja została anulowana.";

            var data = new Dictionary<string, object>
            {
                ["type"] = "RESERVATION_CANCELLED",
                ["reservationId"] = reservationId.ToString(),
                ["isRefund"] = isRefund.ToString(),
                ["deepLink"] = $"{_deepLinkScheme}://reservations",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            };

            var message = new ExpoMessage
            {
                to = pushToken,
                title = "Rezerwacja anulowana",
                body = body,
                data = data,
                sound = "default"
            };

            return await SendExpoNotificationAsync(pushToken, message);
        }

        public async Task<bool> SendCustomNotificationAsync(string pushToken, string title, string body, Dictionary<string, string>? data = null)
        {
            if (string.IsNullOrWhiteSpace(pushToken) || !IsValidExpoToken(pushToken))
            {
                _logger.LogWarning("Invalid Expo token provided for custom notification. Skipping notification.");
                return false;
            }

            var notificationData = new Dictionary<string, object>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    notificationData[item.Key] = item.Value;
                }
            }
            notificationData["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var message = new ExpoMessage
            {
                to = pushToken,
                title = title,
                body = body,
                data = notificationData,
                sound = "default"
            };

            return await SendExpoNotificationAsync(pushToken, message);
        }
    }
}