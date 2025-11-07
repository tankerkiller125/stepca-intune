using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Client for downloading and uploading certificate revocation requests with Intune
/// </summary>
public class IntuneRevocationClient
{
    private const string DEFAULT_SERVICE_VERSION = "5019-05-05";
    private const string CAREQUEST_SERVICE_NAME = "PkiConnectorFEService";
    private const string DOWNLOADREVOCATIONREQUESTS_URL = "CertificateAuthorityRequests/downloadRevocationRequests";
    private const string UPLOADREVOCATIONRESULTS_URL = "CertificateAuthorityRequests/uploadRevocationResults";
    private const int MAXREQUESTS_MAXVALUE = 500;

    private readonly IntuneScepValidationOptions _options;
    private readonly ILogger<IntuneRevocationClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfidentialClientApplication _msalClient;
    private readonly string _serviceVersion;

    public IntuneRevocationClient(
        IntuneScepValidationOptions options,
        ILogger<IntuneRevocationClient> logger,
        HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
        _serviceVersion = DEFAULT_SERVICE_VERSION;

        // Initialize MSAL client
        _msalClient = ConfidentialClientApplicationBuilder
            .Create(_options.AzureAppId)
            .WithClientSecret(_options.AzureAppSecret)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{_options.TenantId}"))
            .Build();
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
            _logger.LogError(ex, "Failed to acquire access token for revocation");
            throw new ScepValidationException("Failed to acquire access token", ex);
        }
    }

    /// <summary>
    /// Downloads a list of certificate revocation requests from Intune
    /// </summary>
    /// <param name="transactionId">Transaction ID for request</param>
    /// <param name="maxRequests">Maximum number of requests to download (1-500)</param>
    /// <param name="issuerName">Optional filter for the issuer name</param>
    /// <returns>List of certificate revocation requests</returns>
    public async Task<List<CARevocationRequest>> DownloadRevocationRequestsAsync(
        string transactionId,
        int maxRequests,
        string? issuerName = null)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));

        if (maxRequests <= 0 || maxRequests > MAXREQUESTS_MAXVALUE)
            throw new ArgumentOutOfRangeException(nameof(maxRequests),
                $"Must be between 1 and {MAXREQUESTS_MAXVALUE}. Requested: {maxRequests}");

        _logger.LogInformation("Downloading revocation requests. Transaction ID: {TransactionId}, Max: {MaxRequests}",
            transactionId, maxRequests);

        // Create request body
        var downloadParams = new
        {
            maxRequests = maxRequests,
            issuerName = issuerName
        };

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        var requestBody = new JObject(
            new JProperty("downloadParameters", JToken.FromObject(downloadParams, JsonSerializer.Create(settings)))
        );

        // Perform download call
        var result = await PostAsync(requestBody, DOWNLOADREVOCATIONREQUESTS_URL, transactionId);

        // Deserialize results
        if (result == null || result["value"] == null)
        {
            throw new ScepValidationException(
                $"Unable to deserialize value returned from Intune. No 'value' property in response. JSON: {result}");
        }

        List<CARevocationRequest>? revocationRequests;
        try
        {
            revocationRequests = result["value"]?.ToObject<List<CARevocationRequest>>();
        }
        catch (JsonException ex)
        {
            throw new ScepValidationException(
                $"Unable to deserialize value returned from Intune. Value: {result}.", ex);
        }

        _logger.LogInformation("Downloaded {Count} revocation requests", revocationRequests?.Count ?? 0);
        return revocationRequests ?? new List<CARevocationRequest>();
    }

    /// <summary>
    /// Uploads revocation results to Intune
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <param name="results">List of revocation results</param>
    public async Task UploadRevocationResultsAsync(string transactionId, List<CARevocationResult> results)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));

        if (results == null || results.Count == 0)
            throw new ArgumentNullException(nameof(results));

        _logger.LogInformation("Uploading {Count} revocation results. Transaction ID: {TransactionId}",
            results.Count, transactionId);

        // Create request body
        var requestBody = new JObject(
            new JProperty("results", JToken.FromObject(results))
        );

        // Perform upload call
        var result = await PostAsync(requestBody, UPLOADREVOCATIONRESULTS_URL, transactionId);

        if (result == null || result["value"] == null)
        {
            throw new ScepValidationException(
                $"Unable to deserialize value returned from Intune. No 'value' property in response. JSON: {result}");
        }

        // Parse result
        if (!bool.TryParse(result["value"]?.ToString(), out bool postSuccessful) || !postSuccessful)
        {
            throw new ScepValidationException(
                $"Results not successfully recorded in Intune. Expected 'true' from service. Received: '{result}'");
        }

        _logger.LogInformation("Successfully uploaded revocation results");
    }

    private async Task<JObject> PostAsync(JObject requestBody, string urlSuffix, string transactionId)
    {
        var activityId = Guid.NewGuid();

        try
        {
            var accessToken = await GetAccessTokenAsync();

            // Construct the full URL
            var url = $"{_options.GraphApiEndpoint}/beta/deviceManagement/{CAREQUEST_SERVICE_NAME}/{_serviceVersion}/{urlSuffix}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("client-request-id", activityId.ToString());
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

            _logger.LogDebug("Posting to Intune: {Url}, Activity ID: {ActivityId}, Transaction ID: {TransactionId}",
                url, activityId, transactionId);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Intune response: Status={StatusCode}, Content={Content}",
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Intune revocation request failed: Status={StatusCode}, Content={Content}",
                    response.StatusCode, responseContent);

                throw new ScepValidationException(
                    $"Intune revocation request failed with status {response.StatusCode}",
                    transactionId,
                    activityId,
                    response.StatusCode.ToString());
            }

            return JObject.Parse(responseContent);
        }
        catch (ScepValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to Intune revocation service");
            throw new ScepValidationException("Failed to communicate with Intune revocation service", ex);
        }
    }
}
