using System;
using System.Text.Json.Serialization;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Represents the result of a certificate authority revocation request
/// </summary>
public class CARevocationResult
{
    /// <summary>
    /// Context for this request
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("requestContext")]
    public string RequestContext { get; set; }

    /// <summary>
    /// Boolean for whether the request was successful
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    /// <summary>
    /// The error code for a failed request
    /// </summary>
    [JsonPropertyName("errorCode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CARequestErrorCode ErrorCode { get; set; }

    /// <summary>
    /// The error message string describing why the request failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="requestContext">Request context</param>
    /// <param name="succeeded">Whether the request succeeded</param>
    /// <param name="errorCode">Error code if failed</param>
    /// <param name="errorMessage">Error message if failed</param>
    public CARevocationResult(string requestContext, bool succeeded, CARequestErrorCode errorCode, string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(requestContext))
        {
            throw new ArgumentNullException(nameof(requestContext));
        }

        if (succeeded && (errorCode != CARequestErrorCode.None || !string.IsNullOrWhiteSpace(errorMessage)))
        {
            throw new ArgumentException($"CARevocationResult cannot be set to Succeeded=true along with an error code or error message. Error Code: {errorCode}; Error Message: {errorMessage};");
        }

        RequestContext = requestContext;
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
