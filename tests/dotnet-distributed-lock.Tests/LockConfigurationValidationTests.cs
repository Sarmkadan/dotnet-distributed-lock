using System;
using System.Collections.Generic;
using FluentAssertions;
using SarmKadan.DistributedLock.Constants;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockConfigurationValidationTests
{
    [Fact]
    public void Validate_ValidConfiguration_ReturnsEmptyList()
    {
        // Arrange
        var config = new LockConfiguration("test-key")
        {
            LockDuration = TimeSpan.FromSeconds(30),
            AcquisitionTimeout = TimeSpan.FromSeconds(5),
            MaxRetries = 1,
            RetryInterval = TimeSpan.FromMilliseconds(100),
            RenewalInterval = TimeSpan.FromSeconds(10),
            AutoRenewal = true
        };

        // Act
        var errors = LockConfigurationValidation.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidKey_ReturnsError()
    {
        // Arrange
        var config = new LockConfiguration("test-key") { LockKey = " " };

        // Act
        var errors = LockConfigurationValidation.Validate(config);

        // Assert
        errors.Should().Contain("Lock key is required and cannot be null or whitespace.");
    }

    [Fact]
    public void IsValid_ValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var config = new LockConfiguration("test-key");

        // Act
        var isValid = LockConfigurationValidation.IsValid(config);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_InvalidConfiguration_ReturnsFalse()
    {
        // Arrange
        var config = new LockConfiguration("test-key") { LockDuration = TimeSpan.FromSeconds(0) };

        // Act
        var isValid = LockConfigurationValidation.IsValid(config);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void EnsureValid_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var config = new LockConfiguration("test-key");

        // Act
        Action act = () => LockConfigurationValidation.EnsureValid(config);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_InvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var config = new LockConfiguration("test-key") { LockDuration = TimeSpan.FromSeconds(0) };

        // Act
        Action act = () => LockConfigurationValidation.EnsureValid(config);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Lock configuration is invalid*");
    }

    [Fact]
    public void Validate_NullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => LockConfigurationValidation.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
