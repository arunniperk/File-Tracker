using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.App.Services;

/// <summary>
/// Creates timestamped .zip backups of the SQLite database and attachments directory.
/// Uses SqliteConnection.BackupDatabase() for safe online backup (Pitfall 1 mitigation).
/// Also supports restore from backup and database integrity checks.
/// </summary>
public class BackupService : IBackupService
{
    private readonly SqliteConnection _db;
    private readonly ILogger<BackupService> _logger;
    private readonly string _attachmentRoot;
    private readonly string _autoBackupRoot;
    private readonly IConfiguration? _config;

    public BackupService(
        SqliteConnection db,
        ILogger<BackupService> logger,
        string? attachmentRoot = null,
        IConfiguration? configuration = null,
        string? autoBackupRoot = null)
    {
        _db = db;
        _logger = logger;
        _config = configuration;
        _attachmentRoot = attachmentRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileTracker", "attachments");
        _autoBackupRoot = autoBackupRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileTracker", "autobackups");
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

    /// <inheritdoc />
    public async Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException(
                $"Backup file not found: {backupFilePath}", backupFilePath);
        }

        ct.ThrowIfCancellationRequested();

        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"ft_restore_{Guid.NewGuid():N}");

        try
        {
            // 1. Extract the backup .zip to a temp directory
            ZipFile.ExtractToDirectory(backupFilePath, tempExtractDir);

            ct.ThrowIfCancellationRequested();

            // 2. Find the .db file in extracted contents
            var dbFiles = Directory.GetFiles(tempExtractDir, "*.db", SearchOption.TopDirectoryOnly);
            if (dbFiles.Length == 0)
            {
                throw new InvalidOperationException(
                    "Backup file does not contain a database file.");
            }
            var backupDbPath = dbFiles[0];

            // 3. Validate the backup .db is a valid SQLite database (T-04-04 mitigation)
            await ValidateBackupDatabaseAsync(backupDbPath, ct);

            // 4. Get current DB path and attachments path
            var currentDbPath = _db.DataSource;
            var currentAttachmentsDir = _attachmentRoot;

            // 5. Close connection to allow file overwrite, then copy backup DB over current DB
            var wasOpen = _db.State == System.Data.ConnectionState.Open;
            if (wasOpen)
            {
                await _db.CloseAsync();
            }

            try
            {
                // Clean up WAL/SHM files that may prevent overwrite
                var walPath = currentDbPath + "-wal";
                var shmPath = currentDbPath + "-shm";
                try { if (File.Exists(walPath)) File.Delete(walPath); } catch { /* best effort */ }
                try { if (File.Exists(shmPath)) File.Delete(shmPath); } catch { /* best effort */ }

                File.Copy(backupDbPath, currentDbPath, overwrite: true);

                // 6. Restore attachments: delete current attachments dir, copy from backup
                var backupAttachmentsDir = Path.Combine(tempExtractDir, "attachments");
                if (Directory.Exists(backupAttachmentsDir))
                {
                    if (Directory.Exists(currentAttachmentsDir))
                    {
                        Directory.Delete(currentAttachmentsDir, recursive: true);
                    }
                    CopyDirectoryRecursive(backupAttachmentsDir, currentAttachmentsDir);
                }

                _logger.LogInformation("Restore completed from backup: {Path}", backupFilePath);
            }
            finally
            {
                // Re-open the connection
                if (wasOpen)
                {
                    await _db.OpenAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not FileNotFoundException
                                   && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Restore failed from {Path}", backupFilePath);
            throw new InvalidOperationException(
                $"Failed to restore from backup {backupFilePath}: {ex.Message}", ex);
        }
        finally
        {
            // Clean up temp extract directory
            try
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up restore temp directory: {Path}", tempExtractDir);
            }
        }
    }

    /// <summary>
    /// Validates that a backup .db file is a valid SQLite database by opening a
    /// temporary connection and running PRAGMA integrity_check (T-04-04 mitigation).
    /// </summary>
    private async Task ValidateBackupDatabaseAsync(string backupDbPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupDbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        await using var backupConn = new SqliteConnection(connectionString);
        await backupConn.OpenAsync(ct);

        await using var cmd = backupConn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = (await cmd.ExecuteScalarAsync(ct)) as string;

        if (result is null || !result.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Backup database is corrupted: {result ?? "null"}");
        }
    }

    /// <inheritdoc />
    public async Task<IntegrityCheckResult> CheckDatabaseIntegrityAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await using var cmd = _db.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = (await cmd.ExecuteScalarAsync(ct)) as string ?? string.Empty;

            var isOk = result.Equals("ok", StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("Database integrity check: {Result}",
                isOk ? "PASS" : "FAIL - " + result);

            return new IntegrityCheckResult
            {
                IsOk = isOk,
                Message = result
            };
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Database integrity check threw exception — treating as FAIL");
            return new IntegrityCheckResult
            {
                IsOk = false,
                Message = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task PerformAutoBackupIfEnabledAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Recursively copies a directory and its contents to a destination path.
    /// </summary>
    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }
}
