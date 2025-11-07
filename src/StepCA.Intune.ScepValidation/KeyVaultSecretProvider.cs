using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Service for retrieving secrets from Azure Key Vault
/// </summary>
public class KeyVaultSecretProvider
{
    private readonly SecretClient? _secretClient;
    private readonly ILogger<KeyVaultSecretProvider> _logger;
    private readonly bool _isEnabled;

    /// <summary>
    /// Creates a new KeyVaultSecretProvider
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL (e.g., https://your-vault.vault.azure.net/). Leave null to disable Key Vault integration.</param>
    /// <param name="logger">Logger instance</param>
    public KeyVaultSecretProvider(string? keyVaultUrl, ILogger<KeyVaultSecretProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            try
            {
                // Use DefaultAzureCredential which supports:
                // - Environment variables
                // - Managed Identity
                // - Visual Studio
                // - Azure CLI
                // - Azure PowerShell
                var credential = new DefaultAzureCredential();
                _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
                _isEnabled = true;
                _logger.LogInformation("Key Vault integration enabled for vault: {VaultUrl}", keyVaultUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Key Vault client for {VaultUrl}. Key Vault integration disabled.", keyVaultUrl);
                _isEnabled = false;
            }
        }
        else
        {
            _isEnabled = false;
            _logger.LogInformation("Key Vault integration is disabled (no vault URL provided)");
        }
    }

    /// <summary>
    /// Gets a secret value. If the value looks like a Key Vault reference, retrieves it from Key Vault.
    /// Otherwise, returns the value as-is.
    /// </summary>
    /// <param name="value">The value or Key Vault secret name</param>
    /// <param name="secretName">Optional explicit secret name if value should be treated as a reference</param>
    /// <returns>The resolved secret value</returns>
    public async Task<string> GetSecretAsync(string value, string? secretName = null)
    {
        // Return empty string for null/empty values
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // If Key Vault is disabled, return the value as-is
        if (!_isEnabled || _secretClient == null)
            return value;

        // If a secret name is explicitly provided, use it
        if (!string.IsNullOrWhiteSpace(secretName))
        {
            return await RetrieveFromKeyVaultAsync(secretName);
        }

        // Check if the value is a Key Vault reference pattern: keyvault:secretName or kv:secretName
        if (value.StartsWith("keyvault:", StringComparison.OrdinalIgnoreCase))
        {
            var kvSecretName = value.Substring("keyvault:".Length);
            return await RetrieveFromKeyVaultAsync(kvSecretName);
        }

        if (value.StartsWith("kv:", StringComparison.OrdinalIgnoreCase))
        {
            var kvSecretName = value.Substring("kv:".Length);
            return await RetrieveFromKeyVaultAsync(kvSecretName);
        }

        // Not a Key Vault reference, return as-is
        return value;
    }

    private async Task<string> RetrieveFromKeyVaultAsync(string secretName)
    {
        if (_secretClient == null)
            throw new InvalidOperationException("Key Vault client is not initialized");

        try
        {
            _logger.LogDebug("Retrieving secret '{SecretName}' from Key Vault", secretName);
            var secret = await _secretClient.GetSecretAsync(secretName);
            _logger.LogInformation("Successfully retrieved secret '{SecretName}' from Key Vault", secretName);
            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Key Vault", secretName);
            throw new InvalidOperationException($"Failed to retrieve secret '{secretName}' from Key Vault", ex);
        }
    }

    /// <summary>
    /// Checks if Key Vault integration is enabled
    /// </summary>
    public bool IsEnabled => _isEnabled;
}
