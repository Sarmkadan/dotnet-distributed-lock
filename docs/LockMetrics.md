# LockMetrics

`LockMetrics` is a metrics tracking class for distributed locks, providing counters and timing information about lock acquisition, renewal, release, and expiration events. It is designed to be thread-safe and provide visibility into the performance and behavior of distributed lock operations.

## API

### Properties

#### `public long TotalAcquisitionAttempts`
The total number of lock acquisition attempts made. This includes both successful and failed attempts.

#### `public long SuccessfulAcquisitions`
The total number of successful lock acquisition attempts.

#### `public long FailedAcquisitions`
The total number of failed lock acquisition attempts.

#### `public long TotalRenewals`
The total number of lock renewal attempts made.

#### `public long SuccessfulRenewals`
The total number of successful lock renewal attempts.

#### `public long FailedRenewals`
The total number of failed lock renewal attempts.

#### `public long TotalReleases`
The total number of lock releases recorded.

#### `public long ExpiredLocks`
The total number of locks that expired before being explicitly released.

#### `public double AverageAcquisitionTimeMs`
The average time, in milliseconds, taken for successful lock acquisitions. Calculated as the total acquisition time divided by the number of successful acquisitions.

#### `public double AverageHoldTimeMs`
The average time, in milliseconds, that locks were held before being released or expiring. Calculated as the total hold time divided by the number of releases and expirations.

#### `public long CurrentActiveLocks`
The current number of active (held) locks.

#### `public DateTime CreatedAt`
The timestamp when this `LockMetrics` instance was created.

#### `public DateTime LastUpdatedAt`
The timestamp when this `LockMetrics` instance was last updated.

### Methods

#### `public LockMetrics()`
Constructs a new `LockMetrics` instance with all counters initialized to zero and timestamps set to the current time.

#### `public void RecordSuccessfulAcquisition()`
Records a successful lock acquisition. Increments `SuccessfulAcquisitions`, updates `CurrentActiveLocks`, and updates the average acquisition time based on the elapsed time since the last update.

#### `public void RecordFailedAcquisition()`
Records a failed lock acquisition. Increments `FailedAcquisitions`.

#### `public void RecordSuccessfulRenewal()`
Records a successful lock renewal. Increments `SuccessfulRenewals`.

#### `public void RecordFailedRenewal()`
Records a failed lock renewal. Increments `FailedRenewals`.

#### `public void RecordRelease()`
Records a lock release. Increments `TotalReleases`, decrements `CurrentActiveLocks`, and updates the average hold time based on the elapsed time since the last update.

#### `public void RecordExpiredLock()`
Records a lock expiration. Increments `ExpiredLocks` and decrements `CurrentActiveLocks`.

## Usage

### Basic Usage
