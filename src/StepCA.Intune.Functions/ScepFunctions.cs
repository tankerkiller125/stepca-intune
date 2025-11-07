using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StepCA.Intune.ScepValidation;

namespace StepCA.Intune.Functions;

/// <summary>
/// Azure Function for processing SCEP certificate requests
/// </summary>
public class ScepFunctions
{
    private readonly ILogger<ScepFunctions> _logger;
    private readonly ScepCertificateService _certificateService;

    public ScepFunctions(
        ILogger<ScepFunctions> logger,
        ScepCertificateService certificateService)
    {
        _logger = logger;
        _certificateService = certificateService;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [Function("HealthCheck")]
    public HttpResponseData HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check requested");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(JsonConvert.SerializeObject(new
        {
            status = "healthy",
            service = "StepCA Intune SCEP Connector",
            version = "1.0.0"
        }));

        return response;
    }

    /// <summary>
    /// Process SCEP certificate request
    /// POST /api/scep/pkiclient.exe with CSR in request body
    /// </summary>
    [Function("ProcessScepRequest")]
    public async Task<HttpResponseData> ProcessScepRequest(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scep/pkiclient.exe")] HttpRequestData req)
    {
        _logger.LogInformation("SCEP certificate request received");

        try
        {
            // Read request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonConvert.DeserializeObject<ScepCertificateRequest>(requestBody);

            if (requestData == null || string.IsNullOrWhiteSpace(requestData.TransactionId) || 
                string.IsNullOrWhiteSpace(requestData.CertificateRequest))
            {
                _logger.LogWarning("Invalid request: Missing required fields");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.WriteString(JsonConvert.SerializeObject(new
                {
                    error = "Missing required fields: TransactionId and CertificateRequest are required"
                }));
                return errorResponse;
            }

            // Process the certificate request
            var certificate = await _certificateService.ProcessCertificateRequestAsync(
                requestData.TransactionId,
                requestData.CertificateRequest,
                requestData.CaConfiguration ?? "default",
                requestData.CertificateAuthority ?? "StepCA"
            );

            // Return the issued certificate
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString(JsonConvert.SerializeObject(new ScepCertificateResponse
            {
                TransactionId = requestData.TransactionId,
                Certificate = certificate.CertificatePem,
                CertificateChain = certificate.CertificateChainPem,
                SerialNumber = certificate.SerialNumber,
                Thumbprint = certificate.Thumbprint,
                ExpirationDate = certificate.ExpirationDate,
                IssuingAuthority = certificate.IssuingAuthority
            }));

            _logger.LogInformation("Certificate issued successfully for transaction: {TransactionId}", 
                requestData.TransactionId);

            return response;
        }
        catch (ScepValidationException ex)
        {
            _logger.LogError(ex, "SCEP validation failed");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.WriteString(JsonConvert.SerializeObject(new
            {
                error = "Validation failed",
                message = ex.Message,
                transactionId = ex.TransactionId,
                activityId = ex.ActivityId,
                errorCode = ex.ErrorCode
            }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process SCEP request");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.WriteString(JsonConvert.SerializeObject(new
            {
                error = "Internal server error",
                message = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Validate SCEP request only (without issuing certificate)
    /// </summary>
    [Function("ValidateScepRequest")]
    public async Task<HttpResponseData> ValidateScepRequest(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scep/validate")] HttpRequestData req)
    {
        _logger.LogInformation("SCEP validation request received");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonConvert.DeserializeObject<ScepValidationRequest>(requestBody);

            if (requestData == null || string.IsNullOrWhiteSpace(requestData.TransactionId) || 
                string.IsNullOrWhiteSpace(requestData.CertificateRequest))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.WriteString(JsonConvert.SerializeObject(new
                {
                    error = "Missing required fields"
                }));
                return errorResponse;
            }

            // Note: IntuneScepValidator needs to be created here to validate
            var intuneValidator = req.FunctionContext.InstanceServices.GetService(typeof(IntuneScepValidator)) as IntuneScepValidator;
            if (intuneValidator == null)
            {
                throw new InvalidOperationException("IntuneScepValidator not available");
            }

            await intuneValidator.ValidateRequestAsync(requestData.TransactionId, requestData.CertificateRequest);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString(JsonConvert.SerializeObject(new
            {
                status = "valid",
                transactionId = requestData.TransactionId
            }));

            return response;
        }
        catch (ScepValidationException ex)
        {
            _logger.LogError(ex, "Validation failed");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.WriteString(JsonConvert.SerializeObject(new
            {
                error = "Validation failed",
                message = ex.Message
            }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate request");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.WriteString(JsonConvert.SerializeObject(new
            {
                error = "Internal server error",
                message = ex.Message
            }));
            return errorResponse;
        }
    }
}

// Request/Response models
public class ScepCertificateRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public string CertificateRequest { get; set; } = string.Empty;
    public string? CaConfiguration { get; set; }
    public string? CertificateAuthority { get; set; }
}

public class ScepCertificateResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Certificate { get; set; } = string.Empty;
    public string CertificateChain { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public string IssuingAuthority { get; set; } = string.Empty;
}

public class ScepValidationRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public string CertificateRequest { get; set; } = string.Empty;
}
