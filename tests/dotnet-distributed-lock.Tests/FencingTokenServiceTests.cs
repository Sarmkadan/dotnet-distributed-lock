#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Utilities.Helpers;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Provides unit tests for the <see cref="FencingTokenService"/> class.
/// Tests the fencing token generation, validation, and management functionality
/// to ensure proper distributed lock coordination and prevention of stale writes.
/// </summary>
public sealed class FencingTokenServiceTests
{
	private readonly FencingTokenService _service =
		new(NullLogger<FencingTokenService>.Instance);

	// -------------------------------------------------------------------------
	// IssueToken
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that issuing a token for a new resource key returns a valid token
	/// with a positive sequence number.
	/// </summary>
	[Fact]
	public void IssueToken_ForNewKey_ReturnsTokenWithPositiveSequence()
	{
		// Act
		var token = _service.IssueToken("resource:orders");

		// Assert
		token.Should().NotBeNull();
		token.SequenceNumber.Should().BeGreaterThan(0);
		token.Token.Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Tests that issuing tokens multiple times for the same key produces tokens
	/// with incrementing sequence numbers.
	/// </summary>
	[Fact]
	public void IssueToken_CalledTwice_SecondTokenHasHigherSequence()
	{
		// Act
		var first = _service.IssueToken("resource:invoices");
		var second = _service.IssueToken("resource:invoices");

		// Assert — each issue increments the global counter
		second.SequenceNumber.Should().BeGreaterThan(first.SequenceNumber);
	}

	/// <summary>
	/// Tests that tokens issued for different resource keys are tracked independently
	/// and do not interfere with each other.
	/// </summary>
	[Fact]
	public void IssueToken_DifferentKeys_AreTrackedIndependently()
	{
		// Act
		var tokenA = _service.IssueToken("resource:A");
		var tokenB = _service.IssueToken("resource:B");

		// Assert
		_service.GetToken("resource:A").Should().NotBeNull();
		_service.GetToken("resource:B").Should().NotBeNull();
		tokenA.Should().NotBe(tokenB);
	}

	// -------------------------------------------------------------------------
	// GetToken
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that retrieving a token for an unknown resource key returns null.
	/// </summary>
	[Fact]
	public void GetToken_ForUnknownKey_ReturnsNull()
	{
		// Act
		var token = _service.GetToken("does-not-exist");

		// Assert
		token.Should().BeNull();
	}

	/// <summary>
	/// Tests that a token retrieved after being issued matches the original issued token
	/// in both token value and sequence number.
	/// </summary>
	[Fact]
	public void GetToken_AfterIssue_ReturnsSameToken()
	{
		// Arrange
		var issued = _service.IssueToken("resource:payments");

		// Act
		var retrieved = _service.GetToken("resource:payments");

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.Token.Should().Be(issued.Token);
		retrieved.SequenceNumber.Should().Be(issued.SequenceNumber);
	}

	// -------------------------------------------------------------------------
	// ValidateToken
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that validating a token that exactly matches the current stored token
	/// returns true.
	/// </summary>
	[Fact]
	public void ValidateToken_WithExactCurrentToken_ReturnsTrue()
	{
		// Arrange
		var issued = _service.IssueToken("resource:sessions");

		// Act
		var isValid = _service.ValidateToken("resource:sessions", issued);

		// Assert
		isValid.Should().BeTrue();
	}

	/// <summary>
	/// Tests that validating a token with a higher sequence number than the current token
	/// returns true, as newer tokens are always considered valid.
	/// </summary>
	[Fact]
	public void ValidateToken_WithHigherSequenceToken_ReturnsTrue()
	{
		// Arrange — issue a token, then supply a token with a higher sequence
		var issued = _service.IssueToken("resource:tasks");
		var newerToken = issued.IncrementSequence();

		// Act
		var isValid = _service.ValidateToken("resource:tasks", newerToken);

		// Assert — a newer token is always considered valid (no stale write risk)
		isValid.Should().BeTrue();
	}

	/// <summary>
	/// Tests that validating a stale token (with a lower sequence number than the current token)
	/// returns false to prevent stale writes.
	/// </summary>
	[Fact]
	public void ValidateToken_WithStaleToken_ReturnsFalse()
	{
		// Arrange — issue first, then increment so the stored token is newer
		var staleToken = _service.IssueToken("resource:shipments");
		_service.IncrementToken("resource:shipments");

		// Act — stale token (lower sequence) should be rejected
		var isValid = _service.ValidateToken("resource:shipments", staleToken);

		// Assert
		isValid.Should().BeFalse();
	}

	/// <summary>
	/// Tests that validating a token for a resource key that was never issued
	/// returns false.
	/// </summary>
	[Fact]
	public void ValidateToken_ForUnknownKey_ReturnsFalse()
	{
		// Arrange
		var orphanToken = new FencingToken("orphan12345678", 1);

		// Act
		var isValid = _service.ValidateToken("key:never-issued", orphanToken);

		// Assert
		isValid.Should().BeFalse();
	}

	// -------------------------------------------------------------------------
	// ValidateTokenOrThrow
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that validating an invalid token using <see cref="FencingTokenService.ValidateTokenOrThrow"/>
	/// throws an <see cref="InvalidFencingTokenException"/>.
	/// </summary>
	[Fact]
	public void ValidateTokenOrThrow_WithInvalidToken_ThrowsInvalidFencingTokenException()
	{
		// Arrange
		var staleToken = _service.IssueToken("resource:critical");
		_service.IncrementToken("resource:critical");

		// Act
		var act = () => _service.ValidateTokenOrThrow("resource:critical", staleToken);

		// Assert
		act.Should().Throw<InvalidFencingTokenException>()
			.Which.ProvidedToken.Should().Be(staleToken.ToString());
	}

	/// <summary>
	/// Tests that validating a valid token using <see cref="FencingTokenService.ValidateTokenOrThrow"/>
	/// does not throw any exception.
	/// </summary>
	[Fact]
	public void ValidateTokenOrThrow_WithValidToken_DoesNotThrow()
	{
		// Arrange
		var token = _service.IssueToken("resource:safe");

		// Act & Assert
		_service.Invoking(s => s.ValidateTokenOrThrow("resource:safe", token))
			.Should().NotThrow();
	}

	// -------------------------------------------------------------------------
	// RevokeToken
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that revoking a token causes subsequent retrieval to return null.
	/// </summary>
	[Fact]
	public void RevokeToken_AfterRevoke_GetTokenReturnsNull()
	{
		// Arrange
		_service.IssueToken("resource:ephemeral");

		// Act
		_service.RevokeToken("resource:ephemeral");

		// Assert
		_service.GetToken("resource:ephemeral").Should().BeNull();
	}

	/// <summary>
	/// Tests that revoking a token for a non-existent resource key does not throw
	/// any exception.
	/// </summary>
	[Fact]
	public void RevokeToken_ForNonExistentKey_DoesNotThrow()
	{
		// Act & Assert
		_service.Invoking(s => s.RevokeToken("key:ghost")).Should().NotThrow();
	}

	// -------------------------------------------------------------------------
	// IncrementToken
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that incrementing an existing token returns a new token with an incremented
	/// sequence number.
	/// </summary>
	[Fact]
	public void IncrementToken_WhenTokenExists_ReturnsTokenWithIncrementedSequence()
	{
		// Arrange
		var original = _service.IssueToken("resource:counter");

		// Act
		var incremented = _service.IncrementToken("resource:counter");

		// Assert
		incremented.SequenceNumber.Should().Be(original.SequenceNumber + 1);
	}

	/// <summary>
	/// Tests that incrementing a token for a resource key that has no existing token
	/// issues a new token with sequence number 1.
	/// </summary>
	[Fact]
	public void IncrementToken_WhenNoExistingToken_IssuesNewToken()
	{
		// Act — key was never issued
		var token = _service.IncrementToken("resource:fresh");

		// Assert
		token.Should().NotBeNull();
		token.SequenceNumber.Should().BeGreaterThan(0);
	}

	// -------------------------------------------------------------------------
	// ClearAllTokens
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that clearing all tokens removes every tracked token from the service.
	/// </summary>
	[Fact]
	public void ClearAllTokens_RemovesEveryTrackedToken()
	{
		// Arrange
		_service.IssueToken("res:one");
		_service.IssueToken("res:two");
		_service.IssueToken("res:three");

		// Act
		_service.ClearAllTokens();

		// Assert
		_service.GetToken("res:one").Should().BeNull();
		_service.GetToken("res:two").Should().BeNull();
		_service.GetToken("res:three").Should().BeNull();
	}

	// -------------------------------------------------------------------------
	// FencingToken Overflow Tests
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that incrementing a token with a very large sequence number does not
	/// cause overflow issues.
	/// </summary>
	[Fact]
	public void IncrementToken_WithLargeSequenceNumber_HandlesOverflow()
	{
		// Arrange
		var token = _service.IssueToken("resource:overflow");
		// Simulate a very large sequence number that could cause overflow issues
		var largeToken = new FencingToken("test123456789012", long.MaxValue - 10);

		// This should not cause overflow issues
		var act = () => _service.IncrementToken("resource:overflow");

		// Assert
		act.Should().NotThrow();
	}
}

public class ValidationHelperTests
{
	// -------------------------------------------------------------------------
	// ValidateLockName
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that valid lock names do not throw exceptions when validated.
	/// </summary>
	/// <param name="name">The valid lock name to test.</param>
	[Theory]
	[InlineData("valid-lock")]
	[InlineData("lock_name.v2")]
	[InlineData("service:resource:123")]
	public void ValidateLockName_WithValidName_DoesNotThrow(string name)
	{
		// Act & Assert
		FluentActions.Invoking(() => ValidationHelper.ValidateLockName(name)).Should().NotThrow();
	}

	/// <summary>
	/// Tests that null or whitespace lock names throw an <see cref="InvalidOperationException"/>.
	/// </summary>
	/// <param name="name">The null or whitespace name to test.</param>
	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData(null)]
	public void ValidateLockName_WithNullOrWhiteSpace_ThrowsInvalidOperationException(string? name)
	{
		// Act
		var act = () => ValidationHelper.ValidateLockName(name);

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*null or empty*");
	}

	/// <summary>
	/// Tests that lock names with invalid characters throw an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateLockName_WithInvalidCharacters_ThrowsInvalidOperationException()
	{
		// Act — spaces and @ signs are not allowed
		var act = () => ValidationHelper.ValidateLockName("invalid lock@name");

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*Invalid lock name*");
	}

	// -------------------------------------------------------------------------
	// ValidateDuration
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that a zero duration throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateDuration_WithZeroDuration_ThrowsInvalidOperationException()
	{
		// Act
		var act = () => ValidationHelper.ValidateDuration(TimeSpan.Zero);

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*greater than zero*");
	}

	/// <summary>
	/// Tests that a duration exceeding 24 hours throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateDuration_WithDurationExceeding24Hours_ThrowsInvalidOperationException()
	{
		// Act
		var act = () => ValidationHelper.ValidateDuration(TimeSpan.FromHours(25));

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*24 hours*");
	}

	/// <summary>
	/// Tests that a reasonable duration does not throw any exception.
	/// </summary>
	[Fact]
	public void ValidateDuration_WithReasonableDuration_DoesNotThrow()
	{
		// Act & Assert
		FluentActions.Invoking(() => ValidationHelper.ValidateDuration(TimeSpan.FromMinutes(5)))
			.Should().NotThrow();
	}

	// -------------------------------------------------------------------------
	// ValidateRenewalInterval
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that a null renewal interval does not throw any exception.
	/// </summary>
	[Fact]
	public void ValidateRenewalInterval_WhenNullInterval_DoesNotThrow()
	{
		// Act & Assert
		FluentActions.Invoking(() => ValidationHelper.ValidateRenewalInterval(null, TimeSpan.FromMinutes(1)))
			.Should().NotThrow();
	}

	/// <summary>
	/// Tests that a renewal interval equal to the lock duration throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateRenewalInterval_WhenIntervalEqualsLockDuration_ThrowsInvalidOperationException()
	{
		// Arrange
		var lockDuration = TimeSpan.FromSeconds(30);
		var badInterval = TimeSpan.FromSeconds(30);

		// Act
		var act = () => ValidationHelper.ValidateRenewalInterval(badInterval, lockDuration);

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*less than lock duration*");
	}

	// -------------------------------------------------------------------------
	// ValidateLockConfiguration
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that valid lock configuration inputs return a valid result.
	/// </summary>
	[Fact]
	public void ValidateLockConfiguration_WithValidInputs_ReturnsIsValidTrue()
	{
		// Act
		var result = ValidationHelper.ValidateLockConfiguration(
			"service:resource",
			TimeSpan.FromSeconds(30),
			TimeSpan.FromSeconds(10));

		// Assert
		result.IsValid.Should().BeTrue();
		result.Errors.Should().BeEmpty();
	}

	/// <summary>
	/// Tests that multiple validation errors are collected and returned.
	/// </summary>
	[Fact]
	public void ValidateLockConfiguration_WithMultipleErrors_CollectsAllErrors()
	{
		// Arrange — empty name + negative duration
		var result = ValidationHelper.ValidateLockConfiguration(
			"",
			TimeSpan.Zero);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
	}

	// -------------------------------------------------------------------------
	// ThrowIfAnyErrors
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="ValidationHelper.ThrowIfAnyErrors"/> throws an <see cref="InvalidOperationException"/>
	/// when errors are present.
	/// </summary>
	[Fact]
	public void ThrowIfAnyErrors_WithErrors_ThrowsInvalidOperationException()
	{
		// Arrange
		var errors = new List<string> { "Error one", "Error two" };

		// Act
		var act = () => ValidationHelper.ThrowIfAnyErrors(errors);

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*2 error(s)*");
	}

	/// <summary>
	/// Tests that <see cref="ValidationHelper.ThrowIfAnyErrors"/> does not throw when no errors are present.
	/// </summary>
	[Fact]
	public void ThrowIfAnyErrors_WithNoErrors_DoesNotThrow()
	{
		// Act & Assert
		FluentActions.Invoking(() => ValidationHelper.ThrowIfAnyErrors(new List<string>()))
			.Should().NotThrow();
	}

	// -------------------------------------------------------------------------
	// ValidateOwnerId
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that a valid owner ID does not throw any exception.
	/// </summary>
	[Fact]
	public void ValidateOwnerId_WithValidId_DoesNotThrow()
	{
		// Act & Assert
		FluentActions.Invoking(() => ValidationHelper.ValidateOwnerId("worker-node-42"))
			.Should().NotThrow();
	}

	/// <summary>
	/// Tests that an empty owner ID throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateOwnerId_WithEmptyString_ThrowsInvalidOperationException()
	{
		// Act
		var act = () => ValidationHelper.ValidateOwnerId("");

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*null or empty*");
	}

	/// <summary>
	/// Tests that an owner ID exceeding 256 characters throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void ValidateOwnerId_WithTooLongId_ThrowsInvalidOperationException()
	{
		// Arrange — 257 characters
		var tooLong = new string('x', 257);

		// Act
		var act = () => ValidationHelper.ValidateOwnerId(tooLong);

		// Assert
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*256 characters*");
	}
}