using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StepCA.Intune.ScepValidation;

/// <summary>
/// Orchestrates the SCEP certificate issuance process with Intune validation and Step-CA
/// </summary>
public class ScepCertificateService
{
    private readonly IntuneScepValidator _intuneValidator;
    private readonly StepCAClient _stepCAClient;
    private readonly ILogger<ScepCertificateService> _logger;

    public ScepCertificateService(
        IntuneScepValidator intuneValidator,
        StepCAClient stepCAClient,
        ILogger<ScepCertificateService> logger)
    {
        _intuneValidator = intuneValidator ?? throw new ArgumentNullException(nameof(intuneValidator));
        _stepCAClient = stepCAClient ?? throw new ArgumentNullException(nameof(stepCAClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Process a SCEP certificate request
    /// </summary>
    /// <param name="transactionId">Transaction ID for the request</param>
    /// <param name="csrBase64">Base64-encoded certificate signing request</param>
    /// <param name="caConfiguration">CA configuration identifier</param>
    /// <param name="certificateAuthority">Certificate authority identifier</param>
    /// <returns>The issued certificate</returns>
    public async Task<IssuedCertificate> ProcessCertificateRequestAsync(
        string transactionId,
        string csrBase64,
        string caConfiguration = "default",
        string certificateAuthority = "StepCA")
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentNullException(nameof(transactionId));
        
        if (string.IsNullOrWhiteSpace(csrBase64))
            throw new ArgumentNullException(nameof(csrBase64));

        _logger.LogInformation("Processing SCEP certificate request. Transaction ID: {TransactionId}", transactionId);

        try
        {
            // Step 1: Validate the request with Intune
            _logger.LogInformation("Step 1: Validating request with Intune");
            await _intuneValidator.ValidateRequestAsync(transactionId, csrBase64);

            // Step 2: Issue certificate from Step-CA
            _logger.LogInformation("Step 2: Issuing certificate from Step-CA");
            var certificate = await _stepCAClient.IssueCertificateAsync(csrBase64);

            // Step 3: Send success notification to Intune
            _logger.LogInformation("Step 3: Sending success notification to Intune");
            await _intuneValidator.SendSuccessNotificationAsync(
                transactionId: transactionId,
                certificateRequest: csrBase64,
                certThumbprint: certificate.Thumbprint,
                certSerialNumber: certificate.SerialNumber,
                certExpirationDate: certificate.ExpirationDate.ToString("o"), // ISO 8601 format
                certIssuingAuthority: certificate.IssuingAuthority,
                caConfiguration: caConfiguration,
                certificateAuthority: certificateAuthority
            );

            _logger.LogInformation("Certificate request processed successfully. Transaction ID: {TransactionId}", transactionId);

            return certificate;
        }
        catch (ScepValidationException ex)
        {
            _logger.LogError(ex, "SCEP validation failed. Transaction ID: {TransactionId}", transactionId);
            
            // Send failure notification to Intune
            try
            {
                var errorDescription = ex.Message ?? "Validation failed";
                await _intuneValidator.SendFailureNotificationAsync(
                    transactionId: transactionId,
                    certificateRequest: csrBase64,
                    hResult: 0x80070001, // Generic failure code
                    errorDescription: errorDescription.Length > 255 
                        ? errorDescription.Substring(0, 255) 
                        : errorDescription
                );
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Failed to send failure notification to Intune");
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process certificate request. Transaction ID: {TransactionId}", transactionId);
            
            // Send failure notification to Intune
            try
            {
                var errorDescription = ex.Message ?? "Certificate issuance failed";
                await _intuneValidator.SendFailureNotificationAsync(
                    transactionId: transactionId,
                    certificateRequest: csrBase64,
                    hResult: 0x80070002, // Generic error code
                    errorDescription: errorDescription.Length > 255 
                        ? errorDescription.Substring(0, 255) 
                        : errorDescription
                );
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Failed to send failure notification to Intune");
            }

            throw;
        }
    }
}
