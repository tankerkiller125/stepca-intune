using System.Text.Json.Serialization;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Represents a certificate authority revocation request from Intune
/// </summary>
public class CARevocationRequest
{
    /// <summary>
    /// Context for this request
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("requestContext")]
    public string RequestContext { get; set; } = string.Empty;

    /// <summary>
    /// Serial number for the certificate to revoke
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issuer name for the certificate to be revoked
    /// </summary>
    [JsonPropertyName("issuerName")]
    public string? IssuerName { get; set; }

    /// <summary>
    /// CA configuration for the certificate to be revoked
    /// </summary>
    [JsonPropertyName("caConfiguration")]
    public string? CaConfiguration { get; set; }
}
