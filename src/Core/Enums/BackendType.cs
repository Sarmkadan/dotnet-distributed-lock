// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Represents the storage backend for distributed locks.
/// </summary>
public enum BackendType
{
    /// <summary>
    /// Redis backend for high-performance distributed locking.
    /// </summary>
    Redis = 0,

    /// <summary>
    /// SQLite backend for lightweight, file-based locking.
    /// </summary>
    SQLite = 1,

    /// <summary>
    /// PostgreSQL backend for SQL-based distributed locking.
    /// </summary>
    PostgreSQL = 2,

    /// <summary>
    /// In-memory backend for testing and development (non-distributed).
    /// </summary>
    InMemory = 3
}
