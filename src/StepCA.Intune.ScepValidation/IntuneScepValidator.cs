using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Validates SCEP certificate requests against Microsoft Intune
/// </summary>
public class IntuneScepValidator
{
    private const string VALIDATION_SERVICE_NAME = "ScepRequestValidationFEService";
    private const string VALIDATION_URL = "ScepActions/validateRequest";
    private const string NOTIFY_SUCCESS_URL = "ScepActions/successNotification";
    private const string NOTIFY_FAILURE_URL = "ScepActions/failureNotification";

    private readonly IntuneScepValidationOptions _options;
    private readonly ILogger<IntuneScepValidator> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfidentialClientApplication _msalClient;

    public IntuneScepValidator(
        IntuneScepValidationOptions options,
        ILogger<IntuneScepValidator> logger,
        HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();

        ValidateOptions();

        // Initialize MSAL client
        _msalClient = ConfidentialClientApplicationBuilder
            .Create(_options.AzureAppId)
            .WithClientSecret(_options.AzureAppSecret)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{_options.TenantId}"))
            .Build();
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.AzureAppId))
            throw new ArgumentException("AzureAppId is required", nameof(_options));
        
        if (string.IsNullOrWhiteSpace(_options.AzureAppSecret))
            throw new ArgumentException("AzureAppSecret is required", nameof(_options));
        
        if (string.IsNullOrWhiteSpace(_options.TenantId))
            throw new ArgumentException("TenantId is required", nameof(_options));
        
        if (string.IsNullOrWhiteSpace(_options.ProviderNameAndVersion))
            throw new ArgumentException("ProviderNameAndVersion is required", nameof(_options));
    }

    /// <summary>
    /// Gets an access token for Microsoft Graph API
    /// </summary>
    private async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var scopes = new[] { _options.IntuneResourceUrl };
            var result = await _msalClient.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire access token");
            throw new ScepValidationException("Failed to acquire access token", ex);
        }
    }

    /// <summary>
    /// Validates whether the given Certificate Request is valid and from Microsoft Intune.
    /// If the request is not valid, an exception will be thrown.
    /// 
    /// IMPORTANT: If an exception is thrown, the SCEP server should not issue a certificate to the client.
    /// </summary>
    /// <param name="transactionId">The transactionId of the Certificate Request</param>
    /// <param name="certificateRequest">Base 64 encoded PKCS10 packet</param>
    public async Task ValidateRequestAsync(string transactionId, string certificateRequest)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));
        
        if (string.IsNullOrWhiteSpace(certificateRequest))
            throw new ArgumentNullException(nameof(certificateRequest));

        _logger.LogInformation("Validating SCEP request with transaction ID: {TransactionId}", transactionId);

        var requestBody = new JObject(
            new JProperty("request", new JObject(
                new JProperty("transactionId", transactionId),
                new JProperty("certificateRequest", certificateRequest),
                new JProperty("callerInfo", _options.ProviderNameAndVersion)
            ))
        );

        await PostToIntuneAsync(requestBody, VALIDATION_URL, transactionId);
        
        _logger.LogInformation("SCEP request validated successfully for transaction ID: {TransactionId}", transactionId);
    }

    /// <summary>
    /// Send a Success notification to the SCEP Service.
    /// 
    /// IMPORTANT: Call this after successfully issuing a certificate.
    /// </summary>
    /// <param name="transactionId">The transactionId of the CSR</param>
    /// <param name="certificateRequest">Base 64 encoded PKCS10 packet</param>
    /// <param name="certThumbprint">Thumbprint of the certificate issued</param>
    /// <param name="certSerialNumber">Serial number of the certificate issued</param>
    /// <param name="certExpirationDate">Certificate expiration date in ISO 8601 format</param>
    /// <param name="certIssuingAuthority">Issuing Authority that issued the certificate</param>
    /// <param name="caConfiguration">CA configuration that issued the certificate</param>
    /// <param name="certificateAuthority">Certificate Authority that issued the certificate</param>
    public async Task SendSuccessNotificationAsync(
        string transactionId,
        string certificateRequest,
        string certThumbprint,
        string certSerialNumber,
        string certExpirationDate,
        string certIssuingAuthority,
        string caConfiguration,
        string certificateAuthority)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));
        
        if (string.IsNullOrWhiteSpace(certificateRequest))
            throw new ArgumentNullException(nameof(certificateRequest));
        
        if (string.IsNullOrWhiteSpace(certThumbprint))
            throw new ArgumentNullException(nameof(certThumbprint));
        
        if (string.IsNullOrWhiteSpace(certSerialNumber))
            throw new ArgumentNullException(nameof(certSerialNumber));
        
        if (string.IsNullOrWhiteSpace(certExpirationDate))
            throw new ArgumentNullException(nameof(certExpirationDate));
        
        if (string.IsNullOrWhiteSpace(certIssuingAuthority))
            throw new ArgumentNullException(nameof(certIssuingAuthority));

        _logger.LogInformation("Sending success notification for transaction ID: {TransactionId}", transactionId);

        var requestBody = new JObject(
            new JProperty("notification", new JObject(
                new JProperty("transactionId", transactionId),
                new JProperty("certificateRequest", certificateRequest),
                new JProperty("certificateThumbprint", certThumbprint),
                new JProperty("certificateSerialNumber", certSerialNumber),
                new JProperty("certificateExpirationDateUtc", certExpirationDate),
                new JProperty("issuingCertificateAuthority", certIssuingAuthority),
                new JProperty("callerInfo", _options.ProviderNameAndVersion),
                new JProperty("caConfiguration", caConfiguration),
                new JProperty("certificateAuthority", certificateAuthority)
            ))
        );

        await PostToIntuneAsync(requestBody, NOTIFY_SUCCESS_URL, transactionId);
        
        _logger.LogInformation("Success notification sent for transaction ID: {TransactionId}", transactionId);
    }

    /// <summary>
    /// Send a Failure notification to the SCEP service.
    /// 
    /// IMPORTANT: If this method is called, the SCEP server should not issue a certificate to the client.
    /// </summary>
    /// <param name="transactionId">The transactionId of the CSR</param>
    /// <param name="certificateRequest">Base 64 encoded PKCS10 packet</param>
    /// <param name="hResult">32-bit error code</param>
    /// <param name="errorDescription">Description of what error occurred (max 255 chars)</param>
    public async Task SendFailureNotificationAsync(
        string transactionId,
        string certificateRequest,
        long hResult,
        string errorDescription)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));
        
        if (string.IsNullOrWhiteSpace(certificateRequest))
            throw new ArgumentNullException(nameof(certificateRequest));
        
        if (string.IsNullOrWhiteSpace(errorDescription))
            throw new ArgumentNullException(nameof(errorDescription));

        _logger.LogWarning("Sending failure notification for transaction ID: {TransactionId}, Error: {ErrorDescription}", 
            transactionId, errorDescription);

        var requestBody = new JObject(
            new JProperty("notification", new JObject(
                new JProperty("transactionId", transactionId),
                new JProperty("certificateRequest", certificateRequest),
                new JProperty("hResult", hResult),
                new JProperty("errorDescription", errorDescription),
                new JProperty("callerInfo", _options.ProviderNameAndVersion)
            ))
        );

        await PostToIntuneAsync(requestBody, NOTIFY_FAILURE_URL, transactionId);
        
        _logger.LogInformation("Failure notification sent for transaction ID: {TransactionId}", transactionId);
    }

    private async Task PostToIntuneAsync(JObject requestBody, string urlSuffix, string transactionId)
    {
        var activityId = Guid.NewGuid();
        
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            // Construct the full URL
            var url = $"{_options.GraphApiEndpoint}/beta/deviceManagement/{VALIDATION_SERVICE_NAME}/{_options.ServiceVersion}/{urlSuffix}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("client-request-id", activityId.ToString());
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

            _logger.LogDebug("Posting to Intune: {Url}, Activity ID: {ActivityId}", url, activityId);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Intune response: Status={StatusCode}, Content={Content}", 
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Intune request failed: Status={StatusCode}, Content={Content}", 
                    response.StatusCode, responseContent);
                
                throw new ScepValidationException(
                    $"Intune validation failed with status {response.StatusCode}",
                    transactionId,
                    activityId,
                    response.StatusCode.ToString());
            }

            // Parse response
            var result = JObject.Parse(responseContent);
            var code = result["code"]?.ToString();
            var errorDescription = result["errorDescription"]?.ToString();

            if (!string.IsNullOrEmpty(code) && code != "Success")
            {
                var errorMessage = $"Intune validation error: Code={code}, Description={errorDescription}";
                _logger.LogError(errorMessage);
                throw new ScepValidationException(errorMessage, transactionId, activityId, code);
            }
        }
        catch (ScepValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to Intune service");
            throw new ScepValidationException("Failed to communicate with Intune service", ex);
        }
    }
}
