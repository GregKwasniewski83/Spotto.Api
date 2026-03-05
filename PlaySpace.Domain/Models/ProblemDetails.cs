using System.Text.Json.Serialization;

namespace PlaySpace.Domain.Models;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs
/// </summary>
public class ProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "about:blank";

    /// <summary>
    /// A short, human-readable summary of the problem type
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP status code
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// A URI reference that identifies the specific occurrence of the problem
    /// </summary>
    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Additional properties for extension data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Extensions { get; set; } = new();
}

/// <summary>
/// TPay-specific Problem Details
/// </summary>
public class TPayProblemDetails : ProblemDetails
{
    [JsonPropertyName("tpayRequestId")]
    public string? TPayRequestId { get; set; }

    [JsonPropertyName("tpayErrors")]
    public List<TPayErrorDetail>? TPayErrors { get; set; }
}

public class TPayErrorDetail
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("fieldName")]
    public string? FieldName { get; set; }

    [JsonPropertyName("devMessage")]
    public string? DevMessage { get; set; }

    [JsonPropertyName("docUrl")]
    public string? DocUrl { get; set; }
}