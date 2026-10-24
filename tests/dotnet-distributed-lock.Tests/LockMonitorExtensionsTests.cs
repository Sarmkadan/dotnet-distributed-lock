using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace dotnet_distributed_lock.Tests
{
    public class LockMonitorExtensionsTests
    {
        // LockMonitor is sealed, so it is exercised directly rather than through a mock.
        private static LockMonitor CreateMonitor(IEnumerable<string>? initialLocks = null)
        {
            var monitor = new LockMonitor(new Mock<ILockService>().Object, NullLogger<LockMonitor>.Instance);
            foreach (var key in initialLocks ?? Enumerable.Empty<string>())
            {
                monitor.RegisterLock(key, "owner", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            }
            return monitor;
        }

        [Fact]
        public void IsLockMonitored_ReturnsTrueWhenPresentAndFalseWhenAbsent()
        {
            var monitor = CreateMonitor(new[] { "key1", "key2" });

            Assert.True(monitor.IsLockMonitored("key1"));
            Assert.False(monitor.IsLockMonitored("missing"));
        }

        [Fact]
        public void IsLockMonitored_NullOrWhiteSpaceKey_ThrowsArgumentException()
        {
            var monitor = CreateMonitor(new[] { "key" });

            Assert.Throws<ArgumentException>(() => monitor.IsLockMonitored(string.Empty));
            Assert.Throws<ArgumentException>(() => monitor.IsLockMonitored("   "));
        }

        [Fact]
        public void IsLockMonitored_NullKey_ThrowsArgumentNullException()
        {
            var monitor = CreateMonitor(new[] { "key" });

            Assert.Throws<ArgumentNullException>(() => monitor.IsLockMonitored(null!));
        }

        [Fact]
        public void IsLockMonitored_NullMonitor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => LockMonitorExtensions.IsLockMonitored(null!, "key"));
        }

        [Fact]
        public void GetMonitoredLockCount_ReturnsCorrectCount()
        {
            var monitor = CreateMonitor(new[] { "a", "b", "c" });
            Assert.Equal(3, monitor.GetMonitoredLockCount());

            var emptyMonitor = CreateMonitor();
            Assert.Equal(0, emptyMonitor.GetMonitoredLockCount());
        }

        [Fact]
        public void GetMonitoredLockCount_NullMonitor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => LockMonitorExtensions.GetMonitoredLockCount(null!));
        }

        [Fact]
        public void HasActiveLocks_ReflectsPresenceOfLocks()
        {
            var withLocks = CreateMonitor(new[] { "x" });
            var withoutLocks = CreateMonitor();

            Assert.True(withLocks.HasActiveLocks());
            Assert.False(withoutLocks.HasActiveLocks());
        }

        [Fact]
        public void HasActiveLocks_NullMonitor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => LockMonitorExtensions.HasActiveLocks(null!));
        }

        [Fact]
        public async Task WaitForLockReleaseAsync_ReturnsTrueWhenLockIsReleasedBeforeTimeout()
        {
            var monitor = CreateMonitor(new[] { "key1" });

            // Release the lock shortly after the wait starts.
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                monitor.UnregisterLock("key1");
            });

            bool result = await monitor.WaitForLockReleaseAsync("key1", TimeSpan.FromSeconds(5));

            Assert.True(result);
        }

        [Fact]
        public async Task WaitForLockReleaseAsync_ReturnsFalseWhenTimeoutExpires()
        {
            // The lock stays monitored for the whole wait.
            var monitor = CreateMonitor(new[] { "key1" });

            bool result = await monitor.WaitForLockReleaseAsync("key1", TimeSpan.FromMilliseconds(200));

            Assert.False(result);
        }

        [Fact]
        public async Task WaitForLockReleaseAsync_NullOrWhiteSpaceKey_ThrowsArgumentException()
        {
            var monitor = CreateMonitor(new[] { "key" });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await monitor.WaitForLockReleaseAsync(string.Empty));

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await monitor.WaitForLockReleaseAsync("   "));
        }

        [Fact]
        public async Task WaitForLockReleaseAsync_NullMonitor_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await LockMonitorExtensions.WaitForLockReleaseAsync(null!, "key"));
        }
    }
}
