using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StepCA.Intune.ScepValidation;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Configure Intune SCEP validation
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new IntuneScepValidationOptions
    {
        AzureAppId = config["Intune:AzureAppId"] ?? string.Empty,
        AzureAppSecret = config["Intune:AzureAppSecret"] ?? string.Empty,
        TenantId = config["Intune:TenantId"] ?? string.Empty,
        ProviderNameAndVersion = config["Intune:ProviderNameAndVersion"] ?? "StepCA-Intune-Connector/1.0",
        IntuneResourceUrl = config["Intune:IntuneResourceUrl"] ?? "https://graph.microsoft.com/.default",
        GraphApiEndpoint = config["Intune:GraphApiEndpoint"] ?? "https://graph.microsoft.com",
        ServiceVersion = config["Intune:ServiceVersion"] ?? "2018-02-20"
    };
});

// Configure Step-CA
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new StepCAOptions
    {
        ServerUrl = config["StepCA:ServerUrl"] ?? string.Empty,
        ProvisionerName = config["StepCA:ProvisionerName"] ?? string.Empty,
        ProvisionerPassword = config["StepCA:ProvisionerPassword"] ?? string.Empty,
        ValidityHours = int.TryParse(config["StepCA:ValidityHours"], out var hours) ? hours : 8760,
        RootCertificatePath = config["StepCA:RootCertificatePath"]
    };
});

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IntuneScepValidator>(sp =>
{
    var options = sp.GetRequiredService<IntuneScepValidationOptions>();
    var logger = sp.GetRequiredService<ILogger<IntuneScepValidator>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new IntuneScepValidator(options, logger, httpClientFactory.CreateClient());
});

builder.Services.AddSingleton<StepCAClient>(sp =>
{
    var options = sp.GetRequiredService<StepCAOptions>();
    var logger = sp.GetRequiredService<ILogger<StepCAClient>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new StepCAClient(options, logger, httpClientFactory.CreateClient());
});

builder.Services.AddSingleton<ScepCertificateService>();

builder.Build().Run();
