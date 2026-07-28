using System;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Backends.SQLite;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class SqliteLockRepositoryJsonExtensionsTests
{
    private static SqliteLockRepository CreateRepository()
    {
        // In‑memory SQLite database; NullLogger avoids needing a real logger.
        const string connectionString = "Data Source=:memory:";
        return new SqliteLockRepository(connectionString, NullLogger<SqliteLockRepository>.Instance);
    }

    [Fact]
    public void ToJson_NullArgument_ThrowsArgumentNullException()
    {
        SqliteLockRepository? repo = null;
        Assert.Throws<ArgumentNullException>(() => repo!.ToJson());
    }

    [Fact]
    public void ToJson_Indents_WhenRequested_ContainsNewLine()
    {
        var repo = CreateRepository();

        string indented = repo.ToJson(indented: true);
        string nonIndented = repo.ToJson(indented: false);

        // Indented JSON should contain at least one newline character.
        Assert.Contains('\n', indented);
        // Non‑indented JSON should not contain newline characters (except possibly in values).
        Assert.DoesNotContain('\n', nonIndented);
    }

    [Fact]
    public void FromJson_NullOrEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SqliteLockRepositoryJsonExtensions.FromJson(null!));
        Assert.Throws<ArgumentException>(() => SqliteLockRepositoryJsonExtensions.FromJson(string.Empty));
        Assert.Throws<ArgumentException>(() => SqliteLockRepositoryJsonExtensions.FromJson("   "));
    }

    [Fact]
    public void FromJson_ValidJson_ReturnsRepository()
    {
        var repo = CreateRepository();

        // Serialize first – this guarantees the JSON matches the expected shape.
        string json = repo.ToJson();

        var deserialized = SqliteLockRepositoryJsonExtensions.FromJson(json);

        Assert.NotNull(deserialized);
        // The deserialized instance should be of the correct type.
        Assert.IsType<SqliteLockRepository>(deserialized);
    }

    [Fact]
    public void TryFromJson_MalformedJson_ReturnsFalseAndNull()
    {
        const string malformed = "{ this is not valid json }";

        bool result = SqliteLockRepositoryJsonExtensions.TryFromJson(malformed, out var value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryFromJson_ValidJson_ReturnsTrueAndInstance()
    {
        var repo = CreateRepository();
        string json = repo.ToJson();

        bool result = SqliteLockRepositoryJsonExtensions.TryFromJson(json, out var value);

        Assert.True(result);
        Assert.NotNull(value);
        Assert.IsType<SqliteLockRepository>(value);
    }
}
