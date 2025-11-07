using System;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Represents an issued certificate from Step-CA
/// </summary>
public class IssuedCertificate
{
    /// <summary>
    /// Certificate in PEM format
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// Certificate chain in PEM format
    /// </summary>
    public string CertificateChainPem { get; set; } = string.Empty;

    /// <summary>
    /// Certificate serial number
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Certificate thumbprint (SHA-1)
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Certificate expiration date
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Issuing Certificate Authority
    /// </summary>
    public string IssuingAuthority { get; set; } = string.Empty;
}
