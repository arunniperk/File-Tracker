using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Services;

namespace FileTracker.App.Services;

/// <summary>
/// Creates timestamped .zip backups of the SQLite database and attachments directory.
/// Uses SqliteConnection.BackupDatabase() for safe online backup (Pitfall 1 mitigation).
/// </summary>
public class BackupService : IBackupService
{
    private readonly SqliteConnection _db;
    private readonly ILogger<BackupService> _logger;
    private readonly string _attachmentRoot;

    public BackupService(
        SqliteConnection db,
        ILogger<BackupService> logger,
        string? attachmentRoot = null)
    {
        _db = db;
        _logger = logger;
        _attachmentRoot = attachmentRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileTracker", "attachments");
    }

    /// <inheritdoc />
    public async Task<string> CreateBackupAsync(string destinationFolder, CancellationToken ct = default)
    {
        if (!Directory.Exists(destinationFolder))
        {
            throw new DirectoryNotFoundException(
                $"Destination folder does not exist: {destinationFolder}");
        }

        ct.ThrowIfCancellationRequested();

        var backupFileName = $"FileTracker_Backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
        var zipPath = Path.Combine(destinationFolder, backupFileName);

        // Create a temp staging directory with a unique name
        var stagingDir = Path.Combine(Path.GetTempPath(), $"ft_backup_{Guid.NewGuid():N}");
        var stagingDbPath = Path.Combine(stagingDir, "filetracker_backup.db");
        var stagingAttachmentsDir = Path.Combine(stagingDir, "attachments");

        try
        {
            Directory.CreateDirectory(stagingDir);
            Directory.CreateDirectory(stagingAttachmentsDir);

            ct.ThrowIfCancellationRequested();

            // 1. Backup the SQLite database using the safe Backup API
            BackupDatabase(stagingDbPath, ct);

            // 2. Copy attachments directory if it exists and is not empty
            CopyAttachments(stagingAttachmentsDir, ct);

            // 3. Create the zip archive from the staging directory
            ct.ThrowIfCancellationRequested();
            ZipFile.CreateFromDirectory(stagingDir, zipPath);

            _logger.LogInformation("Backup created: {Path} ({Size} bytes)",
                zipPath, new FileInfo(zipPath).Length);

            return zipPath;
        }
        catch (OperationCanceledException)
        {
            // Clean up partial zip on cancellation
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* best effort */ }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed to {Path}", zipPath);
            // Clean up partial zip on failure
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* best effort */ }
            throw new InvalidOperationException(
                $"Failed to create backup at {zipPath}: {ex.Message}", ex);
        }
        finally
        {
            // Always clean up the temp staging directory (T-04-02 mitigation)
            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up staging directory: {Path}", stagingDir);
            }
        }
    }

    /// <summary>
    /// Uses SqliteConnection.BackupDatabase() to safely copy the live database
    /// to the staging path. This is the safe online backup API — NOT File.Copy.
    /// </summary>
    private void BackupDatabase(string destinationPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using (var destConn = new SqliteConnection(connectionString))
        {
            destConn.Open();
            _db.BackupDatabase(destConn);
        }

        _logger.LogDebug("Database backed up to {Path}", destinationPath);
    }

    /// <summary>
    /// Copies the attachments directory into the staging directory.
    /// Skips silently if the attachments directory is empty or does not exist.
    /// </summary>
    private void CopyAttachments(string stagingAttachmentsDir, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_attachmentRoot))
        {
            _logger.LogDebug("Attachments directory does not exist, skipping: {Path}", _attachmentRoot);
            return;
        }

        var files = Directory.GetFiles(_attachmentRoot, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            _logger.LogDebug("Attachments directory is empty, skipping");
            return;
        }

        // Copy files preserving directory structure relative to _attachmentRoot
        foreach (var sourceFile in files)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(_attachmentRoot, sourceFile);
            var destFile = Path.Combine(stagingAttachmentsDir, relativePath);

            var destDir = Path.GetDirectoryName(destFile);
            if (destDir is not null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourceFile, destFile, overwrite: true);
        }

        _logger.LogDebug("Copied {Count} attachment files to staging", files.Length);
    }
}
