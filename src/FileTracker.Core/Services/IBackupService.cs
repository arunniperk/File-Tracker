namespace FileTracker.Core.Services;

/// <summary>
/// Contract for creating database backups.
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
}
