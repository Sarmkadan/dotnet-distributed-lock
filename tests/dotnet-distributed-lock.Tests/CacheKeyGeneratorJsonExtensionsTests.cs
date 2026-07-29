using System;
using System.Text.Json;
using Xunit;
using SarmKadan.DistributedLock.Caching;

namespace SarmKadan.DistributedLock.Tests;

public class CacheKeyGeneratorJsonExtensionsTests
{
    [Fact]
    public void ToJson_HappyPath_IndentsFalse_ReturnsCompactJson()
    {
        var key = "myCacheKey";
        var json = CacheKeyGeneratorJsonExtensions.ToJson(key, indented: false);

        Assert.Equal($"\"{key}\"", json);
        Assert.DoesNotContain('\n', json);
    }

    [Fact]
    public void ToJson_HappyPath_IndentsTrue_ReturnsIndentedJson()
    {
        var key = "myCacheKey";
        var json = CacheKeyGeneratorJsonExtensions.ToJson(key, indented: true);

        Assert.Equal($"\"{key}\"", json);
        Assert.Contains('\n', json);
    }

    [Fact]
    public void ToJson_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CacheKeyGeneratorJsonExtensions.ToJson(null!));
    }

    [Fact]
    public void FromJson_HappyPath_ReturnsKey()
    {
        var json = "\"myCacheKey\"";
        var key = CacheKeyGeneratorJsonExtensions.FromJson(json);

        Assert.Equal("myCacheKey", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromJson_NullOrEmpty_ReturnsNull(string? json)
    {
        var key = CacheKeyGeneratorJsonExtensions.FromJson(json!);
        Assert.Null(key);
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsJsonException()
    {
        var invalidJson = "{ this is not valid json }";
        Assert.Throws<JsonException>(() => CacheKeyGeneratorJsonExtensions.FromJson(invalidJson));
    }

    [Fact]
    public void TryFromJson_HappyPath_ReturnsTrueAndKey()
    {
        var json = "\"myCacheKey\"";
        var result = CacheKeyGeneratorJsonExtensions.TryFromJson(json, out var key);

        Assert.True(result);
        Assert.Equal("myCacheKey", key);
    }

    [Fact]
    public void TryFromJson_InvalidJson_ReturnsFalseAndNull()
    {
        var invalidJson = "{ this is not valid json }";
        var result = CacheKeyGeneratorJsonExtensions.TryFromJson(invalidJson, out var key);

        Assert.False(result);
        Assert.Null(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFromJson_NullOrEmpty_ReturnsFalseAndNull(string? json)
    {
        var result = CacheKeyGeneratorJsonExtensions.TryFromJson(json!, out var key);

        Assert.False(result);
        Assert.Null(key);
    }
}
