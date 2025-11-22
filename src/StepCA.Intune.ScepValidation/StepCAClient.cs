using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Client for interacting with Step-CA to issue certificates
/// </summary>
public class StepCAClient
{
    private readonly StepCAOptions _options;
    private readonly ILogger<StepCAClient> _logger;
    private readonly HttpClient _httpClient;

    public StepCAClient(
        StepCAOptions options,
        ILogger<StepCAClient> logger,
        HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();

        ValidateOptions();
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            throw new ArgumentException("ServerUrl is required", nameof(_options));
        
        if (string.IsNullOrWhiteSpace(_options.ProvisionerName))
            throw new ArgumentException("ProvisionerName is required", nameof(_options));
        
        if (string.IsNullOrWhiteSpace(_options.ProvisionerPassword))
            throw new ArgumentException("ProvisionerPassword is required", nameof(_options));
    }

    /// <summary>
    /// Revokes a certificate in Step-CA
    /// </summary>
    /// <param name="serialNumber">Serial number of the certificate to revoke</param>
    /// <param name="reason">Revocation reason (optional)</param>
    /// <returns>True if revocation was successful</returns>
    public async Task<bool> RevokeCertificateAsync(string serialNumber, string reason = "unspecified")
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentNullException(nameof(serialNumber));

        _logger.LogInformation("Revoking certificate from Step-CA. Serial: {SerialNumber}", serialNumber);

        try
        {
            // Prepare the revoke request
            var revokeRequest = new JsonObject
            {
                ["serial"] = serialNumber,
                ["reason"] = reason,
                ["reasonCode"] = 0, // 0 = unspecified
                ["provisioner"] = new JsonObject
                {
                    ["name"] = _options.ProvisionerName,
                    ["password"] = _options.ProvisionerPassword
                }
            };

            var url = $"{_options.ServerUrl}/revoke";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(revokeRequest.ToJsonString(), Encoding.UTF8, "application/json");

            _logger.LogDebug("Posting revocation request to Step-CA: {Url}", url);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Step-CA certificate revocation failed: Status={StatusCode}, Content={Content}",
                    response.StatusCode, responseContent);
                
                // Check if certificate is already revoked by checking status code
                // Step-CA returns 400 Bad Request for already revoked certificates
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // Parse response to check for specific error
                    try
                    {
                        var errorResponse = JsonNode.Parse(responseContent);
                        var errorType = errorResponse?["type"]?.GetValue<string>() ?? "";
                        var errorDetail = errorResponse?["detail"]?.GetValue<string>() ?? "";
                        
                        // Check if error indicates certificate is already revoked
                        if (errorType.Contains("badRequest") && 
                            (errorDetail.Contains("already") || errorDetail.Contains("revoked")))
                        {
                            _logger.LogWarning("Certificate {SerialNumber} is already revoked", serialNumber);
                            return true; // Consider already revoked as success
                        }
                    }
                    catch (JsonException)
                    {
                        // If we can't parse the error, fall through to throw exception
                    }
                }

                throw new StepCAException(
                    $"Step-CA revocation failed: {response.StatusCode} - {responseContent}",
                    (int)response.StatusCode);
            }

            _logger.LogInformation("Certificate revoked successfully. Serial: {SerialNumber}", serialNumber);
            return true;
        }
        catch (StepCAException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke certificate from Step-CA. Serial: {SerialNumber}", serialNumber);
            throw new StepCAException($"Failed to revoke certificate: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Issues a certificate from Step-CA based on the provided CSR
    /// </summary>
    /// <param name="csrBase64">Base64-encoded PKCS#10 certificate signing request</param>
    /// <returns>Issued certificate information</returns>
    public async Task<IssuedCertificate> IssueCertificateAsync(string csrBase64)
    {
        if (string.IsNullOrWhiteSpace(csrBase64))
            throw new ArgumentNullException(nameof(csrBase64));

        _logger.LogInformation("Requesting certificate from Step-CA");

        try
        {
            // Convert base64 CSR to PEM format
            var csrPem = ConvertBase64ToPem(csrBase64, "CERTIFICATE REQUEST");

            // Prepare the sign request
            var signRequest = new JsonObject
            {
                ["csr"] = csrPem,
                ["provisioner"] = new JsonObject
                {
                    ["name"] = _options.ProvisionerName,
                    ["password"] = _options.ProvisionerPassword
                },
                ["validityDuration"] = $"{_options.ValidityHours}h"
            };

            var url = $"{_options.ServerUrl}/sign";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(signRequest.ToJsonString(), Encoding.UTF8, "application/json");

            _logger.LogDebug("Posting certificate request to Step-CA: {Url}", url);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Step-CA certificate issuance failed: Status={StatusCode}, Content={Content}",
                    response.StatusCode, responseContent);
                throw new StepCAException(
                    $"Step-CA returned error: {response.StatusCode} - {responseContent}",
                    (int)response.StatusCode);
            }

            var result = JsonNode.Parse(responseContent);
            
            // Extract certificate information
            var certPem = result?["crt"]?.GetValue<string>();
            var certChainPem = result?["certChain"]?.GetValue<string>() ?? string.Empty;

            if (string.IsNullOrEmpty(certPem))
            {
                throw new StepCAException("Step-CA did not return a certificate");
            }

            // Parse the certificate to extract details
            var cert = ParsePemCertificate(certPem);

            var issuedCert = new IssuedCertificate
            {
                CertificatePem = certPem,
                CertificateChainPem = certChainPem,
                SerialNumber = cert.SerialNumber,
                Thumbprint = cert.Thumbprint,
                ExpirationDate = cert.NotAfter,
                IssuingAuthority = cert.Issuer
            };

            _logger.LogInformation("Certificate issued successfully. Serial: {SerialNumber}, Thumbprint: {Thumbprint}",
                issuedCert.SerialNumber, issuedCert.Thumbprint);

            return issuedCert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue certificate from Step-CA");
            throw;
        }
    }

    private string ConvertBase64ToPem(string base64Data, string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        
        // Add line breaks every 64 characters
        for (int i = 0; i < base64Data.Length; i += 64)
        {
            int length = Math.Min(64, base64Data.Length - i);
            sb.AppendLine(base64Data.Substring(i, length));
        }
        
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private X509Certificate2 ParsePemCertificate(string pemCertificate)
    {
        // Remove PEM headers and decode
        var base64 = pemCertificate
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();

        var certBytes = Convert.FromBase64String(base64);
        return new X509Certificate2(certBytes);
    }
}
