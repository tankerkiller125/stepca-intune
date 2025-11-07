using System;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Exception thrown when Step-CA operations fail
/// </summary>
public class StepCAException : Exception
{
    public int? StatusCode { get; }

    public StepCAException(string message) : base(message)
    {
    }

    public StepCAException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }

    public StepCAException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
