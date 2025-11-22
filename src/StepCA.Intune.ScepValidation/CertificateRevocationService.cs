using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Service for processing certificate revocation requests from Intune
/// </summary>
public class CertificateRevocationService
{
    private readonly IntuneRevocationClient _intuneClient;
    private readonly StepCAClient _stepCAClient;
    private readonly ILogger<CertificateRevocationService> _logger;

    public CertificateRevocationService(
        IntuneRevocationClient intuneClient,
        StepCAClient stepCAClient,
        ILogger<CertificateRevocationService> logger)
    {
        _intuneClient = intuneClient ?? throw new ArgumentNullException(nameof(intuneClient));
        _stepCAClient = stepCAClient ?? throw new ArgumentNullException(nameof(stepCAClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes certificate revocation requests from Intune
    /// </summary>
    /// <param name="maxRequests">Maximum number of requests to process in this batch (1-500)</param>
    /// <param name="issuerName">Optional filter for issuer name</param>
    /// <returns>Number of revocations processed</returns>
    public async Task<int> ProcessRevocationRequestsAsync(int maxRequests = 100, string? issuerName = null)
    {
        var transactionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting revocation processing. Transaction ID: {TransactionId}, Max Requests: {MaxRequests}",
            transactionId, maxRequests);

        try
        {
            // Download revocation requests from Intune
            var requests = await _intuneClient.DownloadRevocationRequestsAsync(transactionId, maxRequests, issuerName);

            if (requests == null || requests.Count == 0)
            {
                _logger.LogInformation("No revocation requests to process");
                return 0;
            }

            _logger.LogInformation("Processing {Count} revocation requests", requests.Count);

            // Process each revocation request
            var results = new List<CARevocationResult>();
            foreach (var request in requests)
            {
                var result = await ProcessSingleRevocationAsync(request);
                results.Add(result);
            }

            // Upload results back to Intune
            if (results.Count > 0)
            {
                await _intuneClient.UploadRevocationResultsAsync(transactionId, results);
                _logger.LogInformation("Successfully processed {Count} revocation requests", results.Count);
            }

            return results.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process revocation requests. Transaction ID: {TransactionId}", transactionId);
            throw;
        }
    }

    private async Task<CARevocationResult> ProcessSingleRevocationAsync(CARevocationRequest request)
    {
        _logger.LogInformation("Processing revocation request. Context: {Context}, Serial: {Serial}",
            request.RequestContext, request.SerialNumber);

        try
        {
            // Revoke the certificate in Step-CA
            bool success = await _stepCAClient.RevokeCertificateAsync(request.SerialNumber);

            if (success)
            {
                _logger.LogInformation("Certificate revoked successfully. Serial: {Serial}", request.SerialNumber);
                return new CARevocationResult(request.RequestContext, true, CARequestErrorCode.None, null);
            }
            else
            {
                _logger.LogWarning("Certificate revocation returned false. Serial: {Serial}", request.SerialNumber);
                return new CARevocationResult(
                    request.RequestContext,
                    false,
                    CARequestErrorCode.NonRetryableServiceException,
                    "Certificate revocation failed");
            }
        }
        catch (StepCAException ex)
        {
            _logger.LogError(ex, "Step-CA error revoking certificate. Serial: {Serial}", request.SerialNumber);

            // Determine if error is retryable
            var errorCode = ex.StatusCode >= 500
                ? CARequestErrorCode.RetryableServiceException
                : CARequestErrorCode.NonRetryableServiceException;

            return new CARevocationResult(
                request.RequestContext,
                false,
                errorCode,
                $"Step-CA error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error revoking certificate. Serial: {Serial}", request.SerialNumber);
            return new CARevocationResult(
                request.RequestContext,
                false,
                CARequestErrorCode.NonRetryableServiceException,
                $"Unexpected error: {ex.Message}");
        }
    }
}
