using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.App.Services;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;
using NotFoundException = FileTracker.Core.Exceptions.NotFoundException;

namespace FileTracker.Tests.Services;

public class AttachmentServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private IDocumentRepository _docRepository = null!;
    private IAttachmentRepository _attachmentRepo = null!;
    private IAttachmentService _attachmentService = null!;
    private string _tempAttachmentRoot = null!;
    private ILogger<DocumentRepository> _docRepoLogger = null!;
    private ILogger<AttachmentRepository> _attachmentRepoLogger = null!;
    private ILogger<AttachmentService> _attachmentServiceLogger = null!;

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
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Documents_OriginalFileNumber
        ON Documents(OriginalFileNumber);
        CREATE TABLE IF NOT EXISTS TrackingSequence (
            Year INTEGER NOT NULL PRIMARY KEY,
            LastNumber INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS Positions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            DisplayOrder INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1
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
        );
        CREATE INDEX IF NOT EXISTS IX_Attachments_DocumentId ON Attachments(DocumentId);";

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = _connection.CreateCommand();
        schemaCmd.CommandText = CreateSchema;
        await schemaCmd.ExecuteNonQueryAsync();

        var loggerFactory = new NullLoggerFactory();
        _docRepoLogger = loggerFactory.CreateLogger<DocumentRepository>();
        _attachmentRepoLogger = loggerFactory.CreateLogger<AttachmentRepository>();
        _attachmentServiceLogger = loggerFactory.CreateLogger<AttachmentService>();

        _docRepository = new DocumentRepository(_connection, _docRepoLogger);
        _attachmentRepo = new AttachmentRepository(_connection, _attachmentRepoLogger);

        _tempAttachmentRoot = Path.Combine(Path.GetTempPath(), $"ft_attachments_{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(_tempAttachmentRoot);

        _attachmentService = new AttachmentService(
            _docRepository,
            _attachmentRepo,
            _attachmentServiceLogger,
            _tempAttachmentRoot);
    }

    public async ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        if (_tempAttachmentRoot is not null && Directory.Exists(_tempAttachmentRoot))
        {
            try { Directory.Delete(_tempAttachmentRoot, recursive: true); } catch { }
        }
    }

    private async Task<Document> SeedDocument(string originalFileNumber = "ATT-TEST-001")
    {
        var doc = new Document
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "Test Subject",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = originalFileNumber,
            TrackingId = "0001/2026",
            Remarks = "Test remarks",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        doc.Id = await _docRepository.InsertAsync(doc);
        return doc;
    }

    private string CreateTempFile(string fileName, string content = "test content", string? extension = null)
    {
        var ext = extension ?? Path.GetExtension(fileName);
        var tempFile = Path.Combine(_tempAttachmentRoot, fileName);
        // Ensure the file has the correct extension
        if (!string.IsNullOrEmpty(ext))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            tempFile = Path.Combine(_tempAttachmentRoot, $"{nameWithoutExt}{ext}");
        }
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private string CreatePdfFile()
    {
        // Create a minimal valid PDF file with magic bytes
        var path = Path.Combine(_tempAttachmentRoot, $"test_pdf_{Guid.NewGuid().ToString("N")}.pdf");
        File.WriteAllBytes(path, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }); // %PDF- magic bytes
        return path;
    }

    private string CreateJpgFile()
    {
        var path = Path.Combine(_tempAttachmentRoot, $"test_jpg_{Guid.NewGuid().ToString("N")}.jpg");
        File.WriteAllBytes(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG magic bytes
        return path;
    }

    private string CreatePngFile()
    {
        var path = Path.Combine(_tempAttachmentRoot, $"test_png_{Guid.NewGuid().ToString("N")}.png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes
        return path;
    }

    private string CreateLargeFile()
    {
        var path = Path.Combine(_tempAttachmentRoot, $"large_file_{Guid.NewGuid().ToString("N")}.pdf");
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        fs.SetLength(11_000_000); // 11 MB — over the 10MB limit
        return path;
    }

    private string CreateExeFile()
    {
        var path = Path.Combine(_tempAttachmentRoot, $"bad_file_{Guid.NewGuid().ToString("N")}.exe");
        File.WriteAllText(path, "not an allowed file");
        return path;
    }

    // ────────────────────────────────────────────────────────────
    // Test 1: AddAttachmentAsync copies file, inserts DB row, returns Attachment
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AddAttachmentAsync_CopiesFileToCorrectSubdirectory_InsertsDbRow_ReturnsWithGeneratedId()
    {
        var doc = await SeedDocument("ATT-ADD-001");
        var sourceFile = CreatePdfFile();

        var result = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.DocumentId.Should().Be(doc.Id);
        // result.FileName should be the original filename (ending with .pdf)
        result.FileName.Should().Be(Path.GetFileName(sourceFile));
        result.FileSize.Should().BeGreaterThan(0);
        result.ContentType.Should().Be("application/pdf");

        // Verify file is in the correct subdirectory
        result.StoragePath.Should().Contain(Path.Combine(_tempAttachmentRoot, doc.Id.ToString()));
        File.Exists(result.StoragePath).Should().BeTrue();

        // Verify DB row was inserted
        var fromDb = await _attachmentRepo.GetByIdAsync(result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.DocumentId.Should().Be(doc.Id);
        fromDb.FileName.Should().Be(result.FileName);
    }

    // ────────────────────────────────────────────────────────────
    // Test 2: AddAttachmentAsync prepends timestamp to prevent collisions
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AddAttachmentAsync_PrependsTimestampToFilename()
    {
        var doc = await SeedDocument("ATT-ADD-002");
        var sourceFile = CreatePdfFile();

        var result = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);

        // The stored filename should contain a date prefix like yyyyMMdd_HHmmss_
        var storedFileName = Path.GetFileName(result.StoragePath);
        storedFileName.Should().MatchRegex(@"^\d{8}_\d{6}_");
        // Original filename should NOT have the timestamp prefix
        result.FileName.Should().NotMatchRegex(@"^\d{8}_\d{6}_");
        result.FileName.Should().Be(Path.GetFileName(sourceFile));
    }

    // ────────────────────────────────────────────────────────────
    // Test 3: AddAttachmentAsync throws NotFoundException for nonexistent document
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AddAttachmentAsync_ThrowsNotFoundException_WhenDocumentIdDoesNotExist()
    {
        var sourceFile = CreatePdfFile();

        var act = () => _attachmentService.AddAttachmentAsync(99999, sourceFile);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*99999*");
    }

    // ────────────────────────────────────────────────────────────
    // Test 4: AddAttachmentAsync rejects non-PDF/JPG/PNG extensions
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AddAttachmentAsync_RejectsExeExtension()
    {
        var doc = await SeedDocument("ATT-ADD-003");
        var sourceFile = CreateExeFile();

        var act = () => _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*extension*");
    }

    [Fact]
    public async Task AddAttachmentAsync_RejectsTxtExtension()
    {
        var doc = await SeedDocument("ATT-ADD-004");
        var sourceFile = CreateTempFile("test.txt", "text file", ".txt");

        var act = () => _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddAttachmentAsync_AcceptsPdfJpgPngExtensions()
    {
        var doc = await SeedDocument("ATT-ADD-005");

        var pdfFile = CreatePdfFile();
        var jpgFile = CreateJpgFile();
        var pngFile = CreatePngFile();

        var pdfResult = await _attachmentService.AddAttachmentAsync(doc.Id, pdfFile);
        var jpgResult = await _attachmentService.AddAttachmentAsync(doc.Id, jpgFile);
        var pngResult = await _attachmentService.AddAttachmentAsync(doc.Id, pngFile);

        pdfResult.Should().NotBeNull();
        jpgResult.Should().NotBeNull();
        pngResult.Should().NotBeNull();

        pdfResult.Id.Should().BeGreaterThan(0);
        jpgResult.Id.Should().BeGreaterThan(0);
        pngResult.Id.Should().BeGreaterThan(0);
    }

    // ────────────────────────────────────────────────────────────
    // Test 5: AddAttachmentAsync rejects files larger than 10MB
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AddAttachmentAsync_RejectsFilesLargerThan10Mb()
    {
        var doc = await SeedDocument("ATT-ADD-006");
        var sourceFile = CreateLargeFile();

        var act = () => _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*size*");
    }

    [Fact]
    public async Task AddAttachmentAsync_AcceptsFileExactly10MbOrLess()
    {
        var doc = await SeedDocument("ATT-ADD-007");
        var sourceFile = CreateJpgFile(); // Small file, well under 10MB

        var result = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    // ────────────────────────────────────────────────────────────
    // Test 6: GetAttachmentsAsync returns all attachments for a documentId
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetAttachmentsAsync_ReturnsAllAttachmentsForDocument_OrderedByCreatedAt()
    {
        var doc = await SeedDocument("ATT-LIST-001");
        var pdfFile = CreatePdfFile();
        var jpgFile = CreateJpgFile();

        var a1 = await _attachmentService.AddAttachmentAsync(doc.Id, pdfFile);
        await Task.Delay(1100); // Ensure different CreatedAt timestamps
        var a2 = await _attachmentService.AddAttachmentAsync(doc.Id, jpgFile);

        var attachments = await _attachmentService.GetAttachmentsAsync(doc.Id);

        attachments.Should().HaveCount(2);
        attachments[0].Id.Should().Be(a2.Id); // Newest first
        attachments[1].Id.Should().Be(a1.Id);
    }

    // ────────────────────────────────────────────────────────────
    // Test 7: GetAttachmentsAsync returns empty list for document with no attachments
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetAttachmentsAsync_ReturnsEmptyList_WhenDocumentHasNoAttachments()
    {
        var doc = await SeedDocument("ATT-LIST-002");

        var attachments = await _attachmentService.GetAttachmentsAsync(doc.Id);

        attachments.Should().NotBeNull();
        attachments.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    // Test 8: RemoveAttachmentAsync deletes physical file AND DB row
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task RemoveAttachmentAsync_DeletesPhysicalFileAndDbRow()
    {
        var doc = await SeedDocument("ATT-REM-001");
        var sourceFile = CreatePdfFile();
        var attachment = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);

        var storagePath = attachment.StoragePath;
        File.Exists(storagePath).Should().BeTrue();

        await _attachmentService.RemoveAttachmentAsync(attachment.Id);

        File.Exists(storagePath).Should().BeFalse("physical file should be deleted");
        var fromDb = await _attachmentRepo.GetByIdAsync(attachment.Id);
        fromDb.Should().BeNull("DB row should be deleted");
    }

    // ────────────────────────────────────────────────────────────
    // Test 9: RemoveAttachmentAsync throws NotFoundException for nonexistent attachment
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task RemoveAttachmentAsync_ThrowsNotFoundException_WhenAttachmentIdDoesNotExist()
    {
        var act = () => _attachmentService.RemoveAttachmentAsync(99999);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*99999*");
    }

    // ────────────────────────────────────────────────────────────
    // Test 10: Attachment storage path is under the root directory
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task AttachmentStoragePath_IsUnderAttachmentRoot()
    {
        var doc = await SeedDocument("ATT-PATH-001");
        var sourceFile = CreatePdfFile();

        var result = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);

        result.StoragePath.Should().StartWith(_tempAttachmentRoot);
        result.StoragePath.Should().Contain(Path.Combine(_tempAttachmentRoot, doc.Id.ToString()));
    }

    // ────────────────────────────────────────────────────────────
    // Test 11: GetAttachmentsAsync returns attachments with FileExists=false when physical file missing
    // ────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetAttachmentsAsync_ReturnsAttachments_WithFileExistsFalse_WhenPhysicalFileMissing()
    {
        var doc = await SeedDocument("ATT-MISS-001");
        var sourceFile = CreatePdfFile();
        var attachment = await _attachmentService.AddAttachmentAsync(doc.Id, sourceFile);

        // Verify file exists initially
        File.Exists(attachment.StoragePath).Should().BeTrue();

        // Delete the physical file but keep the DB row
        File.Delete(attachment.StoragePath);
        File.Exists(attachment.StoragePath).Should().BeFalse();

        var attachments = await _attachmentService.GetAttachmentsAsync(doc.Id);
        attachments.Should().HaveCount(1);
        attachments[0].Id.Should().Be(attachment.Id);
        attachments[0].FileExists.Should().BeFalse("physical file was deleted, but DB row remains");
    }
}
