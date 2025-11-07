using Xunit;
using StepCA.Intune.ScepValidation;

namespace StepCA.Intune.ScepValidation.Tests.Unit;

public class IntuneScepValidationOptionsTests
{
    [Fact]
    public void IntuneScepValidationOptions_HasDefaultValues()
    {
        // Arrange & Act
        var options = new IntuneScepValidationOptions();

        // Assert
        Assert.Equal("StepCA-Intune-Connector/1.0", options.ProviderNameAndVersion);
        Assert.Equal("https://graph.microsoft.com/.default", options.IntuneResourceUrl);
        Assert.Equal("https://graph.microsoft.com", options.GraphApiEndpoint);
        Assert.Equal("2018-02-20", options.ServiceVersion);
    }

    [Fact]
    public void IntuneScepValidationOptions_CanSetProperties()
    {
        // Arrange & Act
        var options = new IntuneScepValidationOptions
        {
            AzureAppId = "test-app-id",
            AzureAppSecret = "test-secret",
            TenantId = "test-tenant",
            ProviderNameAndVersion = "TestProvider/2.0"
        };

        // Assert
        Assert.Equal("test-app-id", options.AzureAppId);
        Assert.Equal("test-secret", options.AzureAppSecret);
        Assert.Equal("test-tenant", options.TenantId);
        Assert.Equal("TestProvider/2.0", options.ProviderNameAndVersion);
    }
}
