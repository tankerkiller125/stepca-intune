using System;
using Xunit;
using StepCA.Intune.ScepValidation;

namespace StepCA.Intune.ScepValidation.Tests.Unit;

public class ScepValidationExceptionTests
{
    [Fact]
    public void ScepValidationException_CanBeCreatedWithMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new ScepValidationException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.TransactionId);
        Assert.Null(exception.ActivityId);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public void ScepValidationException_CanBeCreatedWithInnerException()
    {
        // Arrange
        var message = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new ScepValidationException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }

    [Fact]
    public void ScepValidationException_CanStoreDetails()
    {
        // Arrange
        var message = "Test error message";
        var transactionId = "test-transaction-id";
        var activityId = Guid.NewGuid();
        var errorCode = "ERROR_CODE";

        // Act
        var exception = new ScepValidationException(message, transactionId, activityId, errorCode);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(transactionId, exception.TransactionId);
        Assert.Equal(activityId, exception.ActivityId);
        Assert.Equal(errorCode, exception.ErrorCode);
    }
}
