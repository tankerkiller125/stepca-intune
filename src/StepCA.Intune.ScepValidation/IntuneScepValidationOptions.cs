namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Configuration options for Intune SCEP validation
/// </summary>
public class IntuneScepValidationOptions
{
    /// <summary>
    /// Azure AD Application (Client) ID for authentication
    /// </summary>
    public string AzureAppId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Application (Client) Secret for authentication
    /// </summary>
    public string AzureAppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Provider name and version identifier
    /// </summary>
    public string ProviderNameAndVersion { get; set; } = "StepCA-Intune-Connector/1.0";

    /// <summary>
    /// Intune resource URL (default: https://graph.microsoft.com/.default)
    /// </summary>
    public string IntuneResourceUrl { get; set; } = "https://graph.microsoft.com/.default";

    /// <summary>
    /// Graph API endpoint (default: https://graph.microsoft.com)
    /// </summary>
    public string GraphApiEndpoint { get; set; } = "https://graph.microsoft.com";

    /// <summary>
    /// SCEP validation service version
    /// </summary>
    public string ServiceVersion { get; set; } = "2018-02-20";
}
