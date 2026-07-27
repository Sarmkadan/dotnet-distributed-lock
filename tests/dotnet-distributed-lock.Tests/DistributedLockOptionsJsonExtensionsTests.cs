using System;
using Xunit;
using SarmKadan.DistributedLock.Configuration;

namespace SarmKadan.DistributedLock.Tests;

public class DistributedLockOptionsJsonExtensionsTests
{
    [Fact]
    public void ToJson_WithDefaultOptions_ReturnsNonEmptyJson()
    {
        // Arrange
        var options = new DistributedLockOptions();

        // Act
        string json = options.ToJson();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.StartsWith("{", json);
        Assert.EndsWith("}", json);
    }

    [Fact]
    public void ToJson_WithIndentation_ReturnsIndentedJson()
    {
        // Arrange
        var options = new DistributedLockOptions();

        // Act
        string json = options.ToJson(indented: true);

        // Assert
        // Indented JSON should contain line breaks (environment newline)
        Assert.Contains(Environment.NewLine, json);
    }

    [Fact]
    public void FromJson_ValidJson_ReturnsDeserializedObject()
    {
        // Arrange
        var original = new DistributedLockOptions();
        string json = original.ToJson();

        // Act
        var deserialized = DistributedLockOptionsJsonExtensions.FromJson(json);

        // Assert
        Assert.NotNull(deserialized);
        // Since we don't know the internal properties, we just ensure the type matches
        Assert.IsType<DistributedLockOptions>(deserialized);
    }

    [Fact]
    public void FromJson_NullOrEmpty_ThrowsArgumentException()
    {
        // Null input
        Assert.Throws<ArgumentException>(() => DistributedLockOptionsJsonExtensions.FromJson(null!));

        // Empty string input
        Assert.Throws<ArgumentException>(() => DistributedLockOptionsJsonExtensions.FromJson(string.Empty));
    }

    [Fact]
    public void TryFromJson_ValidJson_ReturnsTrueAndValue()
    {
        // Arrange
        var original = new DistributedLockOptions();
        string json = original.ToJson();

        // Act
        bool success = DistributedLockOptionsJsonExtensions.TryFromJson(json, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        Assert.IsType<DistributedLockOptions>(result);
    }

    [Fact]
    public void TryFromJson_InvalidJson_ReturnsFalseAndNull()
    {
        // Arrange
        string malformedJson = "{ this is not valid json";

        // Act
        bool success = DistributedLockOptionsJsonExtensions.TryFromJson(malformedJson, out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }
}
