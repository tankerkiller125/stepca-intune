namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Error codes for CA request results
/// </summary>
public enum CARequestErrorCode
{
    /// <summary>
    /// No errors occurred
    /// </summary>
    None = 0,

    /// <summary>
    /// General non-retryable service error
    /// </summary>
    NonRetryableServiceException = 4000,

    /// <summary>
    /// Data failed to deserialize correctly (non-retryable)
    /// </summary>
    DataSerializationError = 4001,

    /// <summary>
    /// Data contained invalid parameters (non-retryable)
    /// </summary>
    ParameterDataInvalidError = 4002,

    /// <summary>
    /// Cryptography error attempting to fulfill request (non-retryable)
    /// </summary>
    CryptographyError = 4003,

    /// <summary>
    /// Could not locate the requested certificate (non-retryable)
    /// </summary>
    CertificateNotFoundError = 4004,

    /// <summary>
    /// Conflict processing request (non-retryable), e.g., trying to revoke an already revoked certificate
    /// </summary>
    ConflictError = 4005,

    /// <summary>
    /// Request not supported (non-retryable)
    /// </summary>
    NotSupportedError = 4006,

    /// <summary>
    /// Request is larger than what is allowed by the requesting service (non-retryable)
    /// </summary>
    PayloadTooLargeError = 4007,

    /// <summary>
    /// General retryable service error
    /// </summary>
    RetryableServiceException = 4100,

    /// <summary>
    /// Service unavailable exception (retryable)
    /// </summary>
    ServiceUnavailableException = 4101,

    /// <summary>
    /// Service too busy exception (retryable)
    /// </summary>
    ServiceTooBusyException = 4102,

    /// <summary>
    /// Authentication failure exception (retryable)
    /// </summary>
    AuthenticationException = 4103,
}
