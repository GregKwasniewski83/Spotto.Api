using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Exceptions
{
    public abstract class PlaySpaceException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }
        public Dictionary<string, object> Data { get; }

        protected PlaySpaceException(string errorCode, string message, int statusCode) : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            Data = new Dictionary<string, object>();
        }

        protected PlaySpaceException(string errorCode, string message, int statusCode, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            Data = new Dictionary<string, object>();
        }
    }

    // Business Logic Exceptions
    public class BusinessRuleException : PlaySpaceException
    {
        public BusinessRuleException(string message) : base("BUSINESS_RULE_VIOLATION", message, 400)
        {
        }

        public BusinessRuleException(string message, Exception innerException) 
            : base("BUSINESS_RULE_VIOLATION", message, 400, innerException)
        {
        }
    }

    public class ValidationException : PlaySpaceException
    {
        public Dictionary<string, List<string>> ValidationErrors { get; }

        public ValidationException(string message) : base("VALIDATION_ERROR", message, 400)
        {
            ValidationErrors = new Dictionary<string, List<string>>();
        }

        public ValidationException(Dictionary<string, List<string>> validationErrors) 
            : base("VALIDATION_ERROR", "One or more validation errors occurred.", 400)
        {
            ValidationErrors = validationErrors;
        }
    }

    // Resource Exceptions
    public class NotFoundException : PlaySpaceException
    {
        public string ResourceType { get; }
        public string ResourceId { get; }

        public NotFoundException(string resourceType, string resourceId) 
            : base("RESOURCE_NOT_FOUND", $"{resourceType} with ID '{resourceId}' was not found.", 404)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }

        public NotFoundException(string message) : base("RESOURCE_NOT_FOUND", message, 404)
        {
        }
    }

    public class ConflictException : PlaySpaceException
    {
        public ConflictException(string message) : base("RESOURCE_CONFLICT", message, 409)
        {
        }

        public ConflictException(string message, Exception innerException) 
            : base("RESOURCE_CONFLICT", message, 409, innerException)
        {
        }
    }

    // Authorization Exceptions
    public class UnauthorizedException : PlaySpaceException
    {
        public UnauthorizedException(string message = "Authentication required.") 
            : base("UNAUTHORIZED", message, 401)
        {
        }
    }

    public class ForbiddenException : PlaySpaceException
    {
        public ForbiddenException(string message = "Access denied.") 
            : base("FORBIDDEN", message, 403)
        {
        }
    }

    // TPay Specific Exceptions
    public class TPayException : PlaySpaceException
    {
        public string TransactionId { get; }

        public TPayException(string message) : base("TPAY_ERROR", message, 400)
        {
        }

        public TPayException(string message, string transactionId) : base("TPAY_ERROR", message, 400)
        {
            TransactionId = transactionId;
        }

        public TPayException(string message, Exception innerException) 
            : base("TPAY_ERROR", message, 400, innerException)
        {
        }
    }

    public class TPayAuthenticationException : TPayException
    {
        public TPayAuthenticationException(string message = "TPay authentication failed.") 
            : base($"TPay authentication error: {message}")
        {
        }
    }

    public class TPayTransactionException : TPayException
    {
        public object TPayErrorDetails { get; }
        public string? TPayRequestId { get; }
        public List<TPayErrorInfo>? TPayErrors { get; }

        public TPayTransactionException(string message, string transactionId = null) 
            : base($"TPay transaction error: {message}", transactionId)
        {
        }

        public TPayTransactionException(string message, object tpayErrorDetails, string transactionId = null) 
            : base($"TPay transaction error: {message}", transactionId)
        {
            TPayErrorDetails = tpayErrorDetails;
            
            // Extract structured error information if available
            if (tpayErrorDetails is TPayBusinessRegistrationResponse response)
            {
                TPayRequestId = response.requestId;
                TPayErrors = response.errors?.Select(e => new TPayErrorInfo
                {
                    ErrorCode = e.errorCode,
                    ErrorMessage = e.errorMessage,
                    FieldName = e.fieldName,
                    DevMessage = e.devMessage,
                    DocUrl = e.docUrl
                }).ToList();
            }
        }
    }

    public class TPayErrorInfo
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? FieldName { get; set; }
        public string? DevMessage { get; set; }
        public string? DocUrl { get; set; }
    }

    // Configuration Exceptions
    public class ConfigurationException : PlaySpaceException
    {
        public ConfigurationException(string message) : base("CONFIGURATION_ERROR", message, 500)
        {
        }

        public ConfigurationException(string message, Exception innerException) 
            : base("CONFIGURATION_ERROR", message, 500, innerException)
        {
        }
    }
}