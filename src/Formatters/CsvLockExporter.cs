// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Text;
using SarmKadan.DistributedLock.Core.Models;

/// <summary>
/// Exports lock data to CSV format.
/// Useful for reporting, audit trails, and integration with data analysis tools.
/// Handles escaping and proper CSV formatting.
/// </summary>
public class CsvLockExporter
{
    /// <summary>
    /// Exports a single lock to CSV format (single row).
    /// </summary>
    public static string ExportLock(Lock @lock)
    {
        if (@lock == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(GetCsvHeader());
        sb.Append(LockToCsvRow(@lock));

        return sb.ToString();
    }

    /// <summary>
    /// Exports multiple locks to CSV format.
    /// Includes header row and data rows with proper escaping.
    /// </summary>
    public static string ExportLocks(IEnumerable<Lock> locks)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetCsvHeader());

        foreach (var @lock in locks)
        {
            sb.AppendLine(LockToCsvRow(@lock));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports locks to a stream for large datasets.
    /// More memory-efficient than building entire string first.
    /// </summary>
    public static async Task ExportLocksToStreamAsync(
        IEnumerable<Lock> locks,
        Stream stream)
    {
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            await writer.WriteLineAsync(GetCsvHeader());

            foreach (var @lock in locks)
            {
                await writer.WriteLineAsync(LockToCsvRow(@lock));
            }

            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Exports lock metrics to CSV format.
    /// Includes acquisition stats, timing data, and contention info.
    /// </summary>
    public static string ExportMetrics(IEnumerable<LockMetrics> metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LockName,AcquisitionAttempts,SuccessfulAcquisitions,FailedAcquisitions,SuccessRate,AverageHoldTimeMs,MaxHoldTimeMs,ContentionCount,LastAcquisitionTime");

        foreach (var m in metrics)
        {
            var row = $"{EscapeCsvField(m.Id)}," +
                     $"{m.AcquisitionAttempts}," +
                     $"{m.SuccessfulAcquisitions}," +
                     $"{m.FailedAcquisitions}," +
                     $"{(m.AcquisitionAttempts > 0 ? (double)m.SuccessfulAcquisitions / m.AcquisitionAttempts * 100 : 0):F2}," +
                     $"{m.AverageHoldTimeMs}," +
                     $"{m.MaxHoldTimeMs}," +
                     $"{m.ContentionCount}," +
                     $"{m.LastAcquisitionTime:O}";

            sb.AppendLine(row);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the CSV header row.
    /// </summary>
    private static string GetCsvHeader()
    {
        return "LockId,Name,OwnerId,Status,AcquiredAt,ExpiresAt,RemainingSeconds,FencingToken,AutoRenew";
    }

    /// <summary>
    /// Converts a single lock to a CSV row.
    /// Properly escapes field values containing commas, quotes, or newlines.
    /// </summary>
    private static string LockToCsvRow(Lock @lock)
    {
        var remainingSeconds = (@lock.ExpiresAt - DateTime.UtcNow).TotalSeconds;

        return $"{EscapeCsvField(@lock.Id)}," +
               $"{EscapeCsvField(@lock.Name)}," +
               $"{EscapeCsvField(@lock.OwnerId)}," +
               $"{@lock.Status}," +
               $"{@lock.AcquiredAt:O}," +
               $"{@lock.ExpiresAt:O}," +
               $"{(long)Math.Max(0, remainingSeconds)}," +
               $"{@lock.FencingToken}," +
               $"{@lock.AutoRenew}";
    }

    /// <summary>
    /// Escapes a field value for CSV format.
    /// Wraps fields containing special characters in quotes and escapes internal quotes.
    /// </summary>
    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (!field.Contains(',') && !field.Contains('"') && !field.Contains('\n'))
            return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}

/// <summary>
/// Options for CSV export behavior.
/// </summary>
public class CsvExportOptions
{
    /// <summary>
    /// Include header row in output.
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Delimiter character (typically comma).
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Include timestamp metadata.
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;

    /// <summary>
    /// Encoding for output (UTF-8 by default).
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
