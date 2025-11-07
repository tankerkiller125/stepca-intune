using Xunit;
using StepCA.Intune.ScepValidation;

namespace StepCA.Intune.ScepValidation.Tests.Unit;

public class StepCAOptionsTests
{
    [Fact]
    public void StepCAOptions_HasDefaultValidityHours()
    {
        // Arrange & Act
        var options = new StepCAOptions();

        // Assert
        Assert.Equal(8760, options.ValidityHours); // 1 year
    }

    [Fact]
    public void StepCAOptions_CanSetProperties()
    {
        // Arrange & Act
        var options = new StepCAOptions
        {
            ServerUrl = "https://ca.example.com:9000",
            ProvisionerName = "test-provisioner",
            ProvisionerPassword = "test-password",
            ValidityHours = 720
        };

        // Assert
        Assert.Equal("https://ca.example.com:9000", options.ServerUrl);
        Assert.Equal("test-provisioner", options.ProvisionerName);
        Assert.Equal("test-password", options.ProvisionerPassword);
        Assert.Equal(720, options.ValidityHours);
    }
}
