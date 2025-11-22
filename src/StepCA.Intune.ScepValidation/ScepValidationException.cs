using System;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Exception thrown when SCEP validation fails
/// </summary>
public class ScepValidationException : Exception
{
    public string? TransactionId { get; }
    public Guid? ActivityId { get; }
    public string? ErrorCode { get; }

    public ScepValidationException(string message) : base(message)
    {
    }

    public ScepValidationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }

    public ScepValidationException(string message, string? transactionId, Guid? activityId, string? errorCode) 
        : base(message)
    {
        TransactionId = transactionId;
        ActivityId = activityId;
        ErrorCode = errorCode;
    }
}
