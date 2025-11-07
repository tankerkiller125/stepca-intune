using Newtonsoft.Json;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Represents a certificate authority revocation request from Intune
/// </summary>
public class CARevocationRequest
{
    /// <summary>
    /// Context for this request
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string RequestContext { get; set; } = string.Empty;

    /// <summary>
    /// Serial number for the certificate to revoke
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issuer name for the certificate to be revoked
    /// </summary>
    [JsonProperty(Required = Required.Default)]
    public string? IssuerName { get; set; }

    /// <summary>
    /// CA configuration for the certificate to be revoked
    /// </summary>
    [JsonProperty(Required = Required.Default)]
    public string? CaConfiguration { get; set; }
}
