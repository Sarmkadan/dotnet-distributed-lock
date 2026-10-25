#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using SarmKadan.DistributedLock.Caching;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Tests for <see cref="DistributedCacheExtensions"/>, run against an <see cref="IDistributedCache"/>
/// mock backed by an in-memory dictionary so that stored entries and entry options can be inspected.
/// </summary>
public class DistributedCacheExtensionsTests
{
    private readonly Dictionary<string, byte[]> _store = new();
    private readonly List<DistributedCacheEntryOptions> _setOptions = new();
    private readonly List<string> _removed = new();
    private readonly Mock<IDistributedCache> _mock = new();

    private IDistributedCache Cache => _mock.Object;

    public DistributedCacheExtensionsTests()
    {
        _mock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => Task.FromResult(_store.GetValueOrDefault(key)));

        _mock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback((string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken _) =>
            {
                _store[key] = value;
                _setOptions.Add(options);
            })
            .Returns(Task.CompletedTask);

        _mock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) =>
            {
                _store.Remove(key);
                _removed.Add(key);
            })
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Simple serializable payload used by the JSON round-trip tests.
    /// </summary>
    public sealed record Sample(string Name, int Count);

    /// <summary>
    /// Verifies that a value written with <c>SetAsJsonAsync</c> is read back equal by <c>GetAsJsonAsync</c>.
    /// </summary>
    [Fact]
    public async Task GetAsJsonAsync_AfterSetAsJsonAsync_RoundTripsValue()
    {
        var sample = new Sample("lock-1", 3);

        await Cache.SetAsJsonAsync("k", sample);

        (await Cache.GetAsJsonAsync<Sample>("k")).Should().Be(sample);
    }

    /// <summary>
    /// Verifies that missing, empty, and malformed entries are treated as cache misses (null) rather than errors.
    /// </summary>
    [Fact]
    public async Task GetAsJsonAsync_MissingEmptyOrCorruptEntry_ReturnsNull()
    {
        _store["corrupt"] = "not json"u8.ToArray();
        _store["empty"] = Array.Empty<byte>();

        (await Cache.GetAsJsonAsync<Sample>("missing")).Should().BeNull();
        (await Cache.GetAsJsonAsync<Sample>("empty")).Should().BeNull();
        (await Cache.GetAsJsonAsync<Sample>("corrupt")).Should().BeNull();
    }

    /// <summary>
    /// Verifies that null caches and blank keys are rejected with the documented exception types.
    /// </summary>
    [Fact]
    public async Task GetAsJsonAsync_NullCacheOrBlankKey_Throws()
    {
        IDistributedCache? nullCache = null;

        await FluentActions.Awaiting(() => nullCache!.GetAsJsonAsync<Sample>("k"))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => Cache.GetAsJsonAsync<Sample>("  "))
            .Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies that a positive expiration maps to an absolute expiration, while null or zero sets none,
    /// and that a null value is rejected.
    /// </summary>
    [Fact]
    public async Task SetAsJsonAsync_ExpirationOptions_MapToEntryOptions()
    {
        await Cache.SetAsJsonAsync("none", new Sample("a", 1));
        await Cache.SetAsJsonAsync("timed", new Sample("b", 2), TimeSpan.FromMinutes(5));
        await Cache.SetAsJsonAsync("zero", new Sample("c", 3), TimeSpan.Zero);

        _setOptions[0].AbsoluteExpirationRelativeToNow.Should().BeNull();
        _setOptions[1].AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(5));
        _setOptions[2].AbsoluteExpirationRelativeToNow.Should().BeNull();

        await FluentActions.Awaiting(() => Cache.SetAsJsonAsync<Sample>("null", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that <c>GetOrCreateAsync</c> runs the factory only on a cache miss, and that a null factory
    /// result is returned without being stored.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_CallsFactoryOnlyOnMiss_AndSkipsNullValues()
    {
        var calls = 0;
        Func<Task<Sample?>> factory = () => { calls++; return Task.FromResult<Sample?>(new Sample("made", 7)); };

        var first = await Cache.GetOrCreateAsync("k", factory, TimeSpan.FromMinutes(1));
        var second = await Cache.GetOrCreateAsync("k", factory);

        first.Should().Be(new Sample("made", 7));
        second.Should().Be(first);
        calls.Should().Be(1);
        _setOptions.Should().ContainSingle();

        Func<Task<Sample?>> nothing = () => Task.FromResult<Sample?>(null);
        (await Cache.GetOrCreateAsync("empty", nothing)).Should().BeNull();
        _store.Should().NotContainKey("empty");
    }

    /// <summary>
    /// Verifies that <c>RemoveAsync</c> removes non-blank keys, skips blank ones, and rejects a null array.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_RemovesNonBlankKeys_AndRejectsNullArray()
    {
        await Cache.RemoveAsync("a", "", "  ", "b");

        _removed.Should().Equal("a", "b");

        await FluentActions.Awaiting(() => SarmKadan.DistributedLock.Caching.DistributedCacheExtensions.RemoveAsync(Cache, (string[])null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that <c>ExistsAsync</c> reports only non-empty entries, and that <c>SetExpirationAsync</c>
    /// touches only existing keys with a future expiry.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_And_SetExpirationAsync_RespectStoredEntries()
    {
        _store["present"] = "{}"u8.ToArray();
        _store["empty"] = Array.Empty<byte>();

        (await Cache.ExistsAsync("present")).Should().BeTrue();
        (await Cache.ExistsAsync("missing")).Should().BeFalse();
        (await Cache.ExistsAsync("empty")).Should().BeFalse();

        await Cache.SetExpirationAsync("missing", DateTime.UtcNow.AddMinutes(10));
        _setOptions.Should().BeEmpty();

        await Cache.SetExpirationAsync("present", DateTime.UtcNow.AddMinutes(10));
        _setOptions.Should().ContainSingle().Which.AbsoluteExpirationRelativeToNow!.Value
            .Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5));

        await Cache.SetExpirationAsync("present", DateTime.UtcNow.AddMinutes(-1));
        _setOptions.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that <c>SetWithSlidingExpirationAsync</c> applies the sliding window, rejects non-positive
    /// windows, and that <c>InvalidatePatternAsync</c> validates its pattern without removing entries.
    /// </summary>
    [Fact]
    public async Task SetWithSlidingExpirationAsync_And_InvalidatePatternAsync_ValidateArguments()
    {
        await Cache.SetWithSlidingExpirationAsync("s", new Sample("x", 1), TimeSpan.FromMinutes(2));
        _setOptions.Should().ContainSingle().Which.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(2));

        await FluentActions.Awaiting(() => Cache.SetWithSlidingExpirationAsync("s", new Sample("x", 1), TimeSpan.Zero))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

        await Cache.InvalidatePatternAsync("lock:*");
        _store.Should().ContainKey("s");
        _removed.Should().BeEmpty();

        Action blank = () => _ = Cache.InvalidatePatternAsync(" ");
        blank.Should().Throw<ArgumentException>();
    }
}
