namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Configuration options for Step-CA
/// </summary>
public class StepCAOptions
{
    /// <summary>
    /// Step-CA server URL (e.g., https://ca.example.com:9000)
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Step-CA provisioner name
    /// </summary>
    public string ProvisionerName { get; set; } = string.Empty;

    /// <summary>
    /// Step-CA provisioner password/key
    /// </summary>
    public string ProvisionerPassword { get; set; } = string.Empty;

    /// <summary>
    /// Certificate validity duration in hours (default: 8760 = 1 year)
    /// </summary>
    public int ValidityHours { get; set; } = 8760;

    /// <summary>
    /// Path to the CA root certificate for validation (optional)
    /// </summary>
    public string? RootCertificatePath { get; set; }
}
