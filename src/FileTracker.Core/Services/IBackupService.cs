using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

/// <summary>
/// Contract for creating database backups and restoring from them.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a timestamped .zip backup of the SQLite database and attachments directory
    /// in the specified destination folder.
    /// </summary>
    /// <param name="destinationFolder">The folder where the backup .zip file will be created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full path to the created .zip backup file.</returns>
    Task<string> CreateBackupAsync(string destinationFolder, CancellationToken ct = default);

    /// <summary>
    /// Restores database and attachments from a backup .zip file.
    /// The caller MUST restart the application after this completes successfully (D-05).
    /// </summary>
    /// <param name="backupFilePath">Path to the backup .zip file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default);

    /// <summary>
    /// Runs PRAGMA integrity_check on the current database.
    /// Returns IsOk=false with Message if corruption is detected.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IntegrityCheckResult> CheckDatabaseIntegrityAsync(CancellationToken ct = default);
}
