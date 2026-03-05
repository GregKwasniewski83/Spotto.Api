using System.Net;
using System.Text.Json;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;

namespace PlaySpace.Api.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";

            var problemDetails = CreateProblemDetails(context, exception);
            context.Response.StatusCode = problemDetails.Status;

            var jsonResponse = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private Domain.Models.ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
        {
            return exception switch
            {
                // Most specific exceptions first
                TPayTransactionException tpayEx => CreateTPayTransactionProblemDetails(context, tpayEx),
                TPayAuthenticationException tpayAuthEx => CreateTPayProblemDetails(context, tpayAuthEx),
                TPayException tpayEx => CreateTPayProblemDetails(context, tpayEx),
                ConfigurationException configEx => CreateConfigurationProblemDetails(context, configEx),
                ValidationException validationEx => CreateValidationProblemDetails(context, validationEx),
                TaskCanceledException canceledEx when canceledEx.InnerException is TimeoutException => CreateTimeoutProblemDetails(context, canceledEx),
                ArgumentNullException argNullEx => CreateArgumentProblemDetails(context, argNullEx),
                ArgumentException argEx => CreateArgumentProblemDetails(context, argEx),
                InvalidOperationException invalidOpEx => CreateInvalidOperationProblemDetails(context, invalidOpEx),
                UnauthorizedAccessException unauthorizedEx => CreateUnauthorizedProblemDetails(context, unauthorizedEx),
                TimeoutException timeoutEx => CreateTimeoutProblemDetails(context, timeoutEx),
                HttpRequestException httpEx => CreateExternalServiceProblemDetails(context, httpEx),
                // PlaySpaceException should be after its derived types but before base Exception
                PlaySpaceException playspaceEx => CreatePlaySpaceProblemDetails(context, playspaceEx),
                // Generic fallback
                _ => CreateGenericProblemDetails(context, exception)
            };
        }

        private TPayProblemDetails CreateTPayTransactionProblemDetails(HttpContext context, TPayTransactionException exception)
        {
            return new TPayProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-transaction-failed",
                Title = "TPay Transaction Failed",
                Status = 400,
                Detail = GetPrimaryErrorMessage(exception),
                Instance = context.Request.Path,
                TPayRequestId = exception.TPayRequestId,
                TPayErrors = exception.TPayErrors?.Select(e => new TPayErrorDetail
                {
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    FieldName = e.FieldName,
                    DevMessage = e.DevMessage,
                    DocUrl = e.DocUrl
                }).ToList()
            };
        }

        private Domain.Models.ProblemDetails CreateTPayProblemDetails(HttpContext context, TPayException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-error",
                Title = "TPay Service Error",
                Status = 400,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateValidationProblemDetails(HttpContext context, ValidationException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/validation-error",
                Title = "Validation Error",
                Status = 400,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateArgumentProblemDetails(HttpContext context, Exception exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/invalid-argument",
                Title = "Invalid Argument",
                Status = 400,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateInvalidOperationProblemDetails(HttpContext context, InvalidOperationException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/invalid-operation",
                Title = "Invalid Operation",
                Status = 400,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateUnauthorizedProblemDetails(HttpContext context, UnauthorizedAccessException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/unauthorized",
                Title = "Unauthorized",
                Status = 401,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateConfigurationProblemDetails(HttpContext context, ConfigurationException exception)
        {
            var detail = _environment.IsDevelopment() ? exception.Message : "A configuration error occurred";
            
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/configuration-error",
                Title = "Configuration Error",
                Status = 500,
                Detail = detail,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreatePlaySpaceProblemDetails(HttpContext context, PlaySpaceException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = $"https://playspace.app/problems/{exception.ErrorCode.ToLowerInvariant().Replace("_", "-")}",
                Title = exception.ErrorCode.Replace("_", " ").ToTitleCase(),
                Status = exception.StatusCode,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateTimeoutProblemDetails(HttpContext context, Exception exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/timeout",
                Title = "Request Timeout",
                Status = 408,
                Detail = "The operation timed out",
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateExternalServiceProblemDetails(HttpContext context, HttpRequestException exception)
        {
            return new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/external-service-error",
                Title = "External Service Error",
                Status = 502,
                Detail = "An error occurred while communicating with an external service",
                Instance = context.Request.Path
            };
        }

        private Domain.Models.ProblemDetails CreateGenericProblemDetails(HttpContext context, Exception exception)
        {
            var detail = _environment.IsDevelopment() ? exception.Message : "An error occurred while processing your request";
            var problemDetails = new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/internal-server-error",
                Title = "Internal Server Error",
                Status = 500,
                Detail = detail,
                Instance = context.Request.Path
            };

            if (_environment.IsDevelopment())
            {
                problemDetails.Extensions.Add("exception", new
                {
                    type = exception.GetType().Name,
                    message = exception.Message,
                    stackTrace = exception.StackTrace,
                    traceId = context.TraceIdentifier
                });
            }

            return problemDetails;
        }

        private static string GetPrimaryErrorMessage(TPayTransactionException exception)
        {
            if (exception.TPayErrors?.Count > 0)
            {
                return exception.TPayErrors.First().ErrorMessage;
            }
            return exception.Message;
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
            }
        }
        return string.Join(" ", words);
    }
}