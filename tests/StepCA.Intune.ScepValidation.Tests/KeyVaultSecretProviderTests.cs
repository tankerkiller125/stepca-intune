using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace StepCA.Intune.ScepValidation.Tests.Unit;

public class KeyVaultSecretProviderTests
{
    private readonly Mock<ILogger<KeyVaultSecretProvider>> _mockLogger;

    public KeyVaultSecretProviderTests()
    {
        _mockLogger = new Mock<ILogger<KeyVaultSecretProvider>>();
    }

    [Fact]
    public void KeyVaultSecretProvider_IsDisabled_WhenNoVaultUrlProvided()
    {
        // Arrange & Act
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);

        // Assert
        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public void KeyVaultSecretProvider_IsDisabled_WhenEmptyVaultUrlProvided()
    {
        // Arrange & Act
        var provider = new KeyVaultSecretProvider(string.Empty, _mockLogger.Object);

        // Assert
        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValueAsIs_WhenKeyVaultDisabled()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);
        var testValue = "plain-secret-value";

        // Act
        var result = await provider.GetSecretAsync(testValue);

        // Assert
        Assert.Equal(testValue, result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValueAsIs_WhenNoPrefixUsed()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);
        var testValue = "plain-value";

        // Act
        var result = await provider.GetSecretAsync(testValue);

        // Assert
        Assert.Equal(testValue, result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsEmpty_WhenValueIsNull()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);

        // Act
        var result = await provider.GetSecretAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsEmpty_WhenValueIsEmpty()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);

        // Act
        var result = await provider.GetSecretAsync(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetSecretAsync_HandlesKeyVaultPrefix()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);
        var testValue = "keyvault:MySecret";

        // Act
        var result = await provider.GetSecretAsync(testValue);

        // Assert - when KV is disabled, should return as-is
        Assert.Equal(testValue, result);
    }

    [Fact]
    public async Task GetSecretAsync_HandlesKvPrefix()
    {
        // Arrange
        var provider = new KeyVaultSecretProvider(null, _mockLogger.Object);
        var testValue = "kv:MySecret";

        // Act
        var result = await provider.GetSecretAsync(testValue);

        // Assert - when KV is disabled, should return as-is
        Assert.Equal(testValue, result);
    }
}
