using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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
            var signRequest = new JObject
            {
                ["csr"] = csrPem,
                ["provisioner"] = new JObject
                {
                    ["name"] = _options.ProvisionerName,
                    ["password"] = _options.ProvisionerPassword
                },
                ["validityDuration"] = $"{_options.ValidityHours}h"
            };

            var url = $"{_options.ServerUrl}/sign";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(signRequest.ToString(), Encoding.UTF8, "application/json");

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

            var result = JObject.Parse(responseContent);
            
            // Extract certificate information
            var certPem = result["crt"]?.ToString();
            var certChainPem = result["certChain"]?.ToString() ?? string.Empty;

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
