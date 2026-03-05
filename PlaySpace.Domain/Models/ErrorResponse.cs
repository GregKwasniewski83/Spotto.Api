namespace PlaySpace.Domain.Models
{
    public class ErrorResponse
    {
        public string Error { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public int StatusCode { get; set; }
        public string TraceId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public ErrorResponse()
        {
            Timestamp = DateTime.UtcNow;
            Data = new Dictionary<string, object>();
        }

        public ErrorResponse(string error, string message, int statusCode) : this()
        {
            Error = error;
            Message = message;
            StatusCode = statusCode;
        }
    }

    public class ValidationErrorResponse : ErrorResponse
    {
        public Dictionary<string, List<string>> ValidationErrors { get; set; }

        public ValidationErrorResponse() : base()
        {
            ValidationErrors = new Dictionary<string, List<string>>();
        }

        public ValidationErrorResponse(string message) : base("VALIDATION_ERROR", message, 400)
        {
            ValidationErrors = new Dictionary<string, List<string>>();
        }
    }
}