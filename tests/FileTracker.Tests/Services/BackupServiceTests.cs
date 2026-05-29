using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.App.Services;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class BackupServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private BackupService _backupService = null!;
    private string _tempRoot = null!;
    private string _attachmentsDir = null!;

    private const string CreateSchema = @"
        CREATE TABLE IF NOT EXISTS Documents (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Direction TEXT NOT NULL CHECK(Direction IN ('Incoming', 'Outgoing')),
            Sender TEXT,
            Recipient TEXT,
            Subject TEXT NOT NULL,
            DocumentDate TEXT NOT NULL,
            OriginalFileNumber TEXT NOT NULL,
            TrackingId TEXT,
            Remarks TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            IsDeleted INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS Attachments (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            FileName TEXT NOT NULL,
            StoragePath TEXT NOT NULL,
            FileSize INTEGER NOT NULL DEFAULT 0,
            ContentType TEXT NOT NULL DEFAULT 'application/octet-stream',
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
        );";

    public async ValueTask InitializeAsync()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ft_test_{Guid.NewGuid():N}");
        _attachmentsDir = Path.Combine(_tempRoot, "attachments");
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_attachmentsDir);

        // Use a file-based DB so BackupDatabase() has a real file to read
        var dbPath = Path.Combine(_tempRoot, "filetracker.db");
        _connection = new SqliteConnection($"Data Source={dbPath}");
        await _connection.OpenAsync();

        await using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = _connection.CreateCommand();
        schemaCmd.CommandText = CreateSchema;
        await schemaCmd.ExecuteNonQueryAsync();

        // Seed test data into Documents
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = @"
            INSERT INTO Documents (Direction, Sender, Recipient, Subject, DocumentDate, OriginalFileNumber, TrackingId)
            VALUES ('Incoming', 'Registrar Office', NULL, 'Test Subject 1', '2026-05-01', 'FILE-001', '0001/2026');
            INSERT INTO Documents (Direction, Sender, Recipient, Subject, DocumentDate, OriginalFileNumber, TrackingId)
            VALUES ('Outgoing', NULL, 'Dean Office', 'Test Subject 2', '2026-05-15', 'FILE-002', '0002/2026');";
        await seedCmd.ExecuteNonQueryAsync();

        // Create a test attachment file
        var testFilePath = Path.Combine(_attachmentsDir, "test_attachment.txt");
        await File.WriteAllTextAsync(testFilePath, "Test attachment content");

        var loggerFactory = new NullLoggerFactory();
        _backupService = new BackupService(
            _connection,
            loggerFactory.CreateLogger<BackupService>(),
            attachmentRoot: _attachmentsDir);
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best-effort cleanup */ }
        return ValueTask.CompletedTask;
    }

    // ── Test 1: CreateBackupAsync creates a .zip file containing .db and attachments/ ──

    [Fact]
    public async Task CreateBackupAsync_CreatesZipWithDbAndAttachments()
    {
        // Arrange
        var destDir = Path.Combine(_tempRoot, "backup_dest");
        Directory.CreateDirectory(destDir);

        // Act
        var zipPath = await _backupService.CreateBackupAsync(destDir);

        // Assert: zip file exists
        File.Exists(zipPath).Should().BeTrue();

        // Extract and verify contents
        var extractDir = Path.Combine(_tempRoot, "extracted");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        // Should contain the .db file
        var dbFiles = Directory.GetFiles(extractDir, "*.db", SearchOption.TopDirectoryOnly);
        dbFiles.Should().ContainSingle(f => f.EndsWith("filetracker_backup.db"));

        // Should contain the attachments directory
        var attachmentsSubdir = Path.Combine(extractDir, "attachments");
        Directory.Exists(attachmentsSubdir).Should().BeTrue();

        // Attachments dir should contain the test file
        var attachmentFiles = Directory.GetFiles(attachmentsSubdir, "*", SearchOption.AllDirectories);
        attachmentFiles.Should().ContainSingle(f => f.EndsWith("test_attachment.txt"));
    }

    // ── Test 2: backup .db is a valid SQLite database ──

    [Fact]
    public async Task CreateBackupAsync_BackupDbIsValidSqlite()
    {
        // Arrange
        var destDir = Path.Combine(_tempRoot, "backup_dest2");
        Directory.CreateDirectory(destDir);

        // Act
        var zipPath = await _backupService.CreateBackupAsync(destDir);

        // Extract and verify the .db file can be opened and queried
        var extractDir = Path.Combine(_tempRoot, "extracted2");
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var backupDbPath = Path.Combine(extractDir, "filetracker_backup.db");
        File.Exists(backupDbPath).Should().BeTrue();

        await using var backupConn = new SqliteConnection($"Data Source={backupDbPath}");
        await backupConn.OpenAsync();

        await using var cmd = backupConn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Documents;";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        // Should have the 2 seeded documents
        count.Should().Be(2);
    }

    // ── Test 3: .zip filename matches FileTracker_Backup_YYYY-MM-DD_HHmmss.zip ──

    [Fact]
    public async Task CreateBackupAsync_FilenameMatchesPattern()
    {
        // Arrange
        var destDir = Path.Combine(_tempRoot, "backup_dest3");
        Directory.CreateDirectory(destDir);

        // Act
        var zipPath = await _backupService.CreateBackupAsync(destDir);

        // Assert: filename matches expected pattern
        var fileName = Path.GetFileName(zipPath);
        fileName.Should().MatchRegex(@"^FileTracker_Backup_\d{4}-\d{2}-\d{2}_\d{6}\.zip$");
    }

    // ── Test 4: CreateBackupAsync throws when destination folder does not exist ──

    [Fact]
    public async Task CreateBackupAsync_ThrowsWhenDestinationDoesNotExist()
    {
        // Arrange
        var nonexistentDir = Path.Combine(_tempRoot, "nonexistent_dir");

        // Act
        var act = () => _backupService.CreateBackupAsync(nonexistentDir);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Test 5: CreateBackupAsync succeeds when attachments directory is empty ──

    [Fact]
    public async Task CreateBackupAsync_SucceedsWithEmptyAttachments()
    {
        // Arrange: create a service with an empty attachments directory
        var emptyRoot = Path.Combine(_tempRoot, "empty_test");
        var emptyAttachments = Path.Combine(emptyRoot, "attachments");
        Directory.CreateDirectory(emptyRoot);
        Directory.CreateDirectory(emptyAttachments);

        var emptyDbPath = Path.Combine(emptyRoot, "filetracker.db");
        await using var emptyConn = new SqliteConnection($"Data Source={emptyDbPath}");
        await emptyConn.OpenAsync();

        await using var pragmaCmd = emptyConn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = emptyConn.CreateCommand();
        schemaCmd.CommandText = CreateSchema;
        await schemaCmd.ExecuteNonQueryAsync();

        var loggerFactory = new NullLoggerFactory();
        var emptyBackupService = new BackupService(
            emptyConn,
            loggerFactory.CreateLogger<BackupService>(),
            attachmentRoot: emptyAttachments);

        var destDir = Path.Combine(_tempRoot, "backup_dest5");
        Directory.CreateDirectory(destDir);

        // Act
        var zipPath = await emptyBackupService.CreateBackupAsync(destDir);

        // Assert: zip was created
        File.Exists(zipPath).Should().BeTrue();

        // Extract and verify: db exists, attachments dir may or may not exist (both OK)
        var extractDir = Path.Combine(_tempRoot, "extracted5");
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var dbFiles = Directory.GetFiles(extractDir, "*.db", SearchOption.TopDirectoryOnly);
        dbFiles.Should().ContainSingle(f => f.EndsWith("filetracker_backup.db"));
    }

    // ══════════════════════════════════════════════════════════════════
    //  Plan 04-02 Restore & Integrity Tests (TDD — RED phase)
    // ══════════════════════════════════════════════════════════════════

    // ── Test 6: RestoreFromBackupAsync extracts backup .zip and replaces DB ──

    [Fact]
    public async Task RestoreFromBackupAsync_ReplacesDatabaseFromBackup()
    {
        // Arrange: create a backup of the current state
        var destDir = Path.Combine(_tempRoot, "backup_for_restore");
        Directory.CreateDirectory(destDir);
        var zipPath = await _backupService.CreateBackupAsync(destDir);

        // Mutate the source database: delete all documents
        await using var deleteCmd = _connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Documents;";
        await deleteCmd.ExecuteNonQueryAsync();

        // Verify mutation took effect
        await using var verifyCmd = _connection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM Documents;";
        ((long)(await verifyCmd.ExecuteScalarAsync())!).Should().Be(0);

        // Act: restore from backup
        await _backupService.RestoreFromBackupAsync(zipPath);

        // Re-open the connection to see the restored data
        await _connection.CloseAsync();
        await _connection.OpenAsync();

        // Assert: the 2 documents from the backup are restored
        await using var assertCmd = _connection.CreateCommand();
        assertCmd.CommandText = "SELECT COUNT(*) FROM Documents;";
        var count = (long)(await assertCmd.ExecuteScalarAsync())!;
        count.Should().Be(2);
    }

    // ── Test 7: RestoreFromBackupAsync replaces attachments ──

    [Fact]
    public async Task RestoreFromBackupAsync_ReplacesAttachmentsFromBackup()
    {
        // Arrange: create backup with one attachment file
        var destDir = Path.Combine(_tempRoot, "backup_for_attach_restore");
        Directory.CreateDirectory(destDir);
        var zipPath = await _backupService.CreateBackupAsync(destDir);

        // Delete the attachment file from source
        var existingFile = Path.Combine(_attachmentsDir, "test_attachment.txt");
        File.Delete(existingFile);

        // Add a new file that should be wiped during restore
        var newFile = Path.Combine(_attachmentsDir, "new_file.txt");
        await File.WriteAllTextAsync(newFile, "should be removed");

        // Act: restore from backup
        await _backupService.RestoreFromBackupAsync(zipPath);

        // Assert: original attachment is restored
        File.Exists(existingFile).Should().BeTrue();

        // Assert: new file created after backup is gone
        File.Exists(newFile).Should().BeFalse();
    }

    // ── Test 8: RestoreFromBackupAsync throws when .zip has no .db ──

    [Fact]
    public async Task RestoreFromBackupAsync_ThrowsWhenZipHasNoDatabase()
    {
        // Arrange: create a .zip with no .db file
        var invalidZipDir = Path.Combine(_tempRoot, "invalid_zip_content");
        Directory.CreateDirectory(invalidZipDir);
        await File.WriteAllTextAsync(Path.Combine(invalidZipDir, "some_file.txt"), "not a db");
        await File.WriteAllTextAsync(Path.Combine(invalidZipDir, "notes.md"), "just notes");

        var invalidZipPath = Path.Combine(_tempRoot, "no_db_backup.zip");
        ZipFile.CreateFromDirectory(invalidZipDir, invalidZipPath);

        // Act
        Func<Task> act = async () => await _backupService.RestoreFromBackupAsync(invalidZipPath);

        // Assert: should throw because no .db file in zip
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*database*");
    }

    // ── Test 9: RestoreFromBackupAsync throws FileNotFoundException ──

    [Fact]
    public async Task RestoreFromBackupAsync_ThrowsWhenFileNotFound()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_tempRoot, "does_not_exist.zip");

        // Act
        Func<Task> act = async () => await _backupService.RestoreFromBackupAsync(nonexistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // ── Test 10: CheckDatabaseIntegrityAsync returns IsOk=true for healthy DB ──

    [Fact]
    public async Task CheckDatabaseIntegrityAsync_ReturnsOkForHealthyDatabase()
    {
        // Act
        var result = await _backupService.CheckDatabaseIntegrityAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsOk.Should().BeTrue();
        result.Message.Should().Be("ok");
    }

    // ── Test 11: CheckDatabaseIntegrityAsync returns IsOk=false for corrupted DB ──

    [Fact]
    public async Task CheckDatabaseIntegrityAsync_ReturnsNotOkForCorruptedDatabase()
    {
        // Arrange: create a non-database file and try integrity check on it
        var corruptRoot = Path.Combine(_tempRoot, "corrupt_db_test");
        Directory.CreateDirectory(corruptRoot);
        var corruptDbPath = Path.Combine(corruptRoot, "corrupt.db");
        await File.WriteAllTextAsync(corruptDbPath, "this is not a valid SQLite database file");

        // SQLite will detect corruption when we run PRAGMA on a non-db file
        await using var corruptConn = new SqliteConnection($"Data Source={corruptDbPath};Mode=ReadWriteCreate");
        await corruptConn.OpenAsync();

        // Running any SQL on a non-db file should produce errors via PRAGMA
        var loggerFactory = new NullLoggerFactory();
        var corruptService = new BackupService(
            corruptConn,
            loggerFactory.CreateLogger<BackupService>());

        // Act
        var result = await corruptService.CheckDatabaseIntegrityAsync();

        // Assert: a non-database file should not pass integrity check
        result.IsOk.Should().BeFalse();
    }
}
