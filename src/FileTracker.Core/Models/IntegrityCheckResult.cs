namespace FileTracker.Core.Models;

/// <summary>
/// Result of a database integrity check operation.
/// Used by IBackupService.CheckDatabaseIntegrityAsync (Plan 02).
/// </summary>
public class IntegrityCheckResult
{
    /// <summary>True if the integrity check passed without errors.</summary>
    public bool IsOk { get; set; }

    /// <summary>Human-readable result message from the integrity check.</summary>
    public string Message { get; set; } = string.Empty;
}
