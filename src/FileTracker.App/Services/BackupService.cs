using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Services;

namespace FileTracker.App.Services;

/// <summary>
/// Stub — will be implemented in the GREEN phase of TDD.
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

    public Task<string> CreateBackupAsync(string destinationFolder, CancellationToken ct = default)
    {
        throw new NotImplementedException("BackupService not yet implemented — RED phase");
    }
}
