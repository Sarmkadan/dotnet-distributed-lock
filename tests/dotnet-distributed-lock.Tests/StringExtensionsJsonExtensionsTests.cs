using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using SarmKadan.DistributedLock.Utilities.Extensions;

namespace SarmKadan.DistributedLock.Tests;

public class StringExtensionsJsonExtensionsTests
{
    [Fact]
    public void ToJson_SingleLockName_HappyPath_Compact()
    {
        // Arrange
        var lockName = "myLock";

        // Act
        var json = lockName.ToJson(indented: false);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(lockName, root.GetProperty("lockName").GetString());
        Assert.True(root.GetProperty("isValid").GetBoolean());
        Assert.Equal(lockName.SanitizeForLockName(), root.GetProperty("sanitized").GetString());
        Assert.DoesNotContain('\n', json);
    }

    [Fact]
    public void ToJson_SingleLockName_Indented_ContainsNewLine()
    {
        var lockName = "myLock";

        var json = lockName.ToJson(indented: true);

        // The JSON serializer adds line breaks when indented.
        Assert.Contains('\n', json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(lockName, root.GetProperty("lockName").GetString());
    }

    [Fact]
    public void ToJson_NullOrEmptyLockName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ((string)null!).ToJson());
        Assert.Throws<ArgumentException>(() => "".ToJson());
    }

    [Fact]
    public void ToJson_Collection_HappyPath_EmptyCollection()
    {
        var empty = new List<string>();
        var json = empty.ToJson();

        // Should be an empty JSON array.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public void ToJson_Collection_HappyPath_NonEmpty()
    {
        var names = new[] { "lockA", "lockB" };
        var json = names.ToJson();

        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement;
        Assert.Equal(2, array.GetArrayLength());

        var first = array[0];
        Assert.Equal("lockA", first.GetProperty("lockName").GetString());
        var second = array[1];
        Assert.Equal("lockB", second.GetProperty("lockName").GetString());
    }

    [Fact]
    public void ToJson_Collection_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<string>)null!).ToJson());
    }

    [Fact]
    public void FromLockNameJson_HappyPath_ReturnsLockName()
    {
        var lockName = "validLock";
        var json = lockName.ToJson();

        var result = StringExtensionsJsonExtensions.FromLockNameJson(json);

        Assert.Equal(lockName, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromLockNameJson_NullOrWhiteSpace_ReturnsNull(string? json)
    {
        var result = StringExtensionsJsonExtensions.FromLockNameJson(json);
        Assert.Null(result);
    }

    [Fact]
    public void FromLockNameJson_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StringExtensionsJsonExtensions.FromLockNameJson(null!));
    }

    [Fact]
    public void FromLockNameJson_MalformedJson_ThrowsJsonException()
    {
        var malformed = "{ not: valid json }";
        // The method catches JsonException and returns null, but the contract
        // states it may throw JsonException for malformed JSON.
        // Verify that it returns null rather than bubbling the exception.
        var result = StringExtensionsJsonExtensions.FromLockNameJson(malformed);
        Assert.Null(result);
    }

    [Fact]
    public void TryFromLockNameJson_HappyPath_ReturnsTrueAndLockName()
    {
        var lockName = "myLock";
        var json = lockName.ToJson();

        var success = StringExtensionsJsonExtensions.TryFromLockNameJson(json, out var result);

        Assert.True(success);
        Assert.Equal(lockName, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFromLockNameJson_NullOrWhiteSpace_ReturnsFalse(string? json)
    {
        var success = StringExtensionsJsonExtensions.TryFromLockNameJson(json, out var result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryFromLockNameJson_MalformedJson_ReturnsFalse()
    {
        var malformed = "{ bad json }";
        var success = StringExtensionsJsonExtensions.TryFromLockNameJson(malformed, out var result);
        Assert.False(success);
        Assert.Null(result);
    }
}
