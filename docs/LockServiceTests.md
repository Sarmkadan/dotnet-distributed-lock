# LockServiceTests
The `LockServiceTests` class is a test suite designed to verify the functionality of the `LockService` class in the `dotnet-distributed-lock` project. It provides a comprehensive set of tests to ensure that the `LockService` behaves correctly under various scenarios, including successful and failed lock acquisitions, renewals, releases, and queries.

## API
The `LockServiceTests` class contains the following public members:
* `LockServiceTests`: The constructor for the test class.
* `Constructor_WithNullRepository_ThrowsArgumentNullException`: Tests that the constructor throws an `ArgumentNullException` when the repository is null.
* `Constructor_WithNullLogger_ThrowsArgumentNullException`: Tests that the constructor throws an `ArgumentNullException` when the logger is null.
* `TryAcquireAsync_WhenRepositoryGrantsLock_ReturnsTrueWithLock`: Tests that `TryAcquireAsync` returns `true` with a lock when the repository grants the lock.
* `TryAcquireAsync_WhenRepositoryDeniesLock_ReturnsFalseWithErrorMessage`: Tests that `TryAcquireAsync` returns `false` with an error message when the repository denies the lock.
* `TryAcquireAsync_WhenRepositoryThrows_ReturnsFalseWithExceptionMessage`: Tests that `TryAcquireAsync` returns `false` with an exception message when the repository throws an exception.
* `RenewAsync_WhenRepositoryRenews_ReturnsTrue`: Tests that `RenewAsync` returns `true` when the repository renews the lock.
* `RenewAsync_WhenRepositoryReturnsFalse_ReturnsFalse`: Tests that `RenewAsync` returns `false` when the repository returns `false`.
* `RenewAsync_WhenRepositoryThrows_ReturnsFalse`: Tests that `RenewAsync` returns `false` when the repository throws an exception.
* `ReleaseAsync_WhenLockNotFound_ReturnsFalse`: Tests that `ReleaseAsync` returns `false` when the lock is not found.
* `ReleaseAsync_WhenLockFoundAndReleased_ReturnsTrue`: Tests that `ReleaseAsync` returns `true` when the lock is found and released.
* `IsLockedAsync_WhenRepositoryReturnsTrue_ReturnsTrue`: Tests that `IsLockedAsync` returns `true` when the repository returns `true`.
* `IsLockedAsync_WhenRepositoryThrows_ReturnsFalse`: Tests that `IsLockedAsync` returns `false` when the repository throws an exception.
* `GetAllActiveLockAsync_WhenRepositoryReturnsLocks_ReturnsThem`: Tests that `GetAllActiveLockAsync` returns the locks when the repository returns them.
* `GetAllActiveLockAsync_WhenRepositoryThrows_ReturnsEmptyEnumerable`: Tests that `GetAllActiveLockAsync` returns an empty enumerable when the repository throws an exception.
* `GetMetrics_AfterSuccessfulAcquisition_ReflectsAcquisitionCount`: Tests that `GetMetrics` reflects the acquisition count after a successful acquisition.
* `AcquireAsync_WhenKeyDoesNotExist_ReturnsTrue`: Tests that `AcquireAsync` returns `true` when the key does not exist.
* `AcquireAsync_WhenKeyAlreadyHeldAndNotExpired_ReturnsFalse`: Tests that `AcquireAsync` returns `false` when the key is already held and not expired.
* `AcquireAsync_WhenExistingLockIsExpired_AllowsReacquisition`: Tests that `AcquireAsync` allows reacquisition when the existing lock is expired.
* `GetByKeyAsync_WhenKeyDoesNotExist_ReturnsNull`: Tests that `GetByKeyAsync` returns `null` when the key does not exist.

## Usage
Here are two examples of using the `LockServiceTests` class:
```csharp
// Example 1: Testing lock acquisition
var lockService = new LockServiceTests();
await lockService.TryAcquireAsync_WhenRepositoryGrantsLock_ReturnsTrueWithLock();

// Example 2: Testing lock renewal
var lockService = new LockServiceTests();
await lockService.RenewAsync_WhenRepositoryRenews_ReturnsTrue();
```

## Notes
The `LockServiceTests` class is designed to be thread-safe, allowing multiple tests to run concurrently. However, some tests may have dependencies on the state of the repository or logger, and should be run sequentially to avoid interference. Additionally, some tests may throw exceptions or return error messages, which should be handled accordingly in the test code. The `LockServiceTests` class does not handle these exceptions or errors, and it is the responsibility of the test writer to handle them as needed.
