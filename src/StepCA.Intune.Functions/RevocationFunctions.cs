using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StepCA.Intune.ScepValidation;

namespace StepCA.Intune.Functions;

/// <summary>
/// Azure Functions for processing certificate revocations
/// </summary>
public class RevocationFunctions
{
    private readonly ILogger<RevocationFunctions> _logger;
    private readonly CertificateRevocationService _revocationService;

    public RevocationFunctions(
        ILogger<RevocationFunctions> logger,
        CertificateRevocationService revocationService)
    {
        _logger = logger;
        _revocationService = revocationService;
    }

    /// <summary>
    /// Timer-triggered function that runs once every 24 hours to process certificate revocations
    /// NCRONTAB expression: "0 0 0 * * *" runs once daily at midnight UTC
    /// For testing more frequently, use "0 0 * * * *" to run every hour
    /// </summary>
    [Function("ProcessRevocations")]
    public async Task ProcessRevocations(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Certificate revocation processing started at: {Time}", DateTime.UtcNow);

        try
        {
            // Process up to 500 revocation requests (maximum allowed by Intune API)
            int processed = await _revocationService.ProcessRevocationRequestsAsync(maxRequests: 500);

            _logger.LogInformation("Certificate revocation processing completed. Processed: {Count} revocations", processed);

            if (timerInfo.ScheduleStatus != null)
            {
                _logger.LogInformation("Next timer schedule at: {NextRun}", timerInfo.ScheduleStatus.Next);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during certificate revocation processing");
            // Don't throw - we want the timer to continue running
        }
    }

    /// <summary>
    /// Manual trigger for processing revocations (useful for testing or on-demand processing)
    /// This function is disabled by default and should only be invoked manually or via configuration
    /// </summary>
    [Function("ManualRevocationTrigger")]
    public async Task ManualRevocationTrigger(
        [TimerTrigger("0 0 0 31 12 *", RunOnStartup = false)] TimerInfo timerInfo) // Dec 31st only - effectively manual trigger
    {
        _logger.LogInformation("Manual revocation trigger invoked at: {Time}", DateTime.UtcNow);

        try
        {
            int processed = await _revocationService.ProcessRevocationRequestsAsync(maxRequests: 100);
            _logger.LogInformation("Manual revocation processing completed. Processed: {Count} revocations", processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual revocation processing");
            throw;
        }
    }
}
