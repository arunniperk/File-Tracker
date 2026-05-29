using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class DocumentServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DocumentRepository _repository = null!;
    private DocumentService _service = null!;

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
        );";

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
        _repository = new DocumentRepository(_connection, loggerFactory.CreateLogger<DocumentRepository>());
        _service = new DocumentService(_repository, _connection, loggerFactory.CreateLogger<DocumentService>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RegisterAsync_WithValidIncomingDto_ReturnsDocumentWithIdAndDirection()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Registrar Office",
            Recipient = null,
            Subject = "Admission Letter",
            DocumentDate = new DateTime(2026, 5, 29),
            OriginalFileNumber = "REG/2026/001",
            Remarks = "Test remarks"
        };

        var result = await _service.RegisterAsync(dto);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Direction.Should().Be(DocumentDirection.Incoming);
        result.Sender.Should().Be("Registrar Office");
        result.Subject.Should().Be("Admission Letter");
        result.OriginalFileNumber.Should().Be("REG/2026/001");
        result.Remarks.Should().Be("Test remarks");
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WithMissingSubject_ThrowsArgumentException()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "FILE-001"
        };

        var act = () => _service.RegisterAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*subject*");
    }

    [Fact]
    public async Task RegisterAsync_WithWhitespaceSubject_ThrowsArgumentException()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "   ",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "FILE-001"
        };

        var act = () => _service.RegisterAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterAsync_WithMissingOriginalFileNumber_ThrowsArgumentException()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "Valid Subject",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = ""
        };

        var act = () => _service.RegisterAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*file number*");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDocumentsOrderedByCreatedAtDesc()
    {
        var dto1 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 1",
            Subject = "First Document",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "REG-001"
        };
        var dto2 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 2",
            Subject = "Second Document",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "REG-002"
        };

        var doc1 = await _service.RegisterAsync(dto1);
        await Task.Delay(1100);
        var doc2 = await _service.RegisterAsync(dto2);

        var allDocs = await _service.GetAllAsync();

        allDocs.Should().HaveCount(2);
        allDocs[0].Id.Should().Be(doc2.Id);
        allDocs[1].Id.Should().Be(doc1.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsDocument()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "Test Subject",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "REG-GET-001"
        };
        var saved = await _service.RegisterAsync(dto);

        var result = await _service.GetByIdAsync(saved.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(saved.Id);
        result.Subject.Should().Be("Test Subject");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(99999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_MapsDirectionEnumToStringCorrectly()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Direction Test",
            Subject = "Testing Direction Mapping",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = "REG-DIR-001"
        };

        var result = await _service.RegisterAsync(dto);

        result.Direction.Should().Be(DocumentDirection.Incoming);
    }

    // ──────────────────────────────────────────────
    // Tracking ID tests (Task 1 — RED phase)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_GeneratesTrackingId_FormatD4SlashYear()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Test Sender",
            Subject = "Tracking Test",
            DocumentDate = new DateTime(2026, 3, 15),
            OriginalFileNumber = "TRK-FMT-001",
            Remarks = "First document of 2026"
        };

        var result = await _service.RegisterAsync(dto);

        result.TrackingId.Should().Be("0001/2026");
    }

    [Fact]
    public async Task RegisterAsync_GeneratesSequentialTrackingId_ForSameYear()
    {
        var dto1 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 1",
            Subject = "First Doc",
            DocumentDate = new DateTime(2026, 6, 1),
            OriginalFileNumber = "TRK-SEQ-001"
        };
        var dto2 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 2",
            Subject = "Second Doc",
            DocumentDate = new DateTime(2026, 6, 2),
            OriginalFileNumber = "TRK-SEQ-002"
        };

        var doc1 = await _service.RegisterAsync(dto1);
        var doc2 = await _service.RegisterAsync(dto2);

        doc1.TrackingId.Should().Be("0001/2026");
        doc2.TrackingId.Should().Be("0002/2026");
    }

    [Fact]
    public async Task RegisterAsync_ResetsTrackingId_WhenYearChanges()
    {
        // Seed a document in 2025 first
        var dto2025 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 2025",
            Subject = "2025 Document",
            DocumentDate = new DateTime(2025, 12, 20),
            OriginalFileNumber = "TRK-YR-001"
        };
        var doc2025 = await _service.RegisterAsync(dto2025);
        doc2025.TrackingId.Should().Be("0001/2025");

        // Now register in 2026 — should reset to 0001
        var dto2026 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender 2026",
            Subject = "2026 Document",
            DocumentDate = new DateTime(2026, 1, 10),
            OriginalFileNumber = "TRK-YR-002"
        };
        var doc2026 = await _service.RegisterAsync(dto2026);
        doc2026.TrackingId.Should().Be("0001/2026");
    }

    [Fact]
    public async Task RegisterAsync_RollsBackSequence_OnDuplicateFileNumber()
    {
        // Register a doc — tracking ID 0001/2026
        var dto1 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender",
            Subject = "Rollback Test 1",
            DocumentDate = new DateTime(2026, 5, 1),
            OriginalFileNumber = "TRK-RB-001"
        };
        var doc1 = await _service.RegisterAsync(dto1);
        doc1.TrackingId.Should().Be("0001/2026");

        // Try to register with the SAME file number — should fail due to UNIQUE constraint
        var dto2 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender",
            Subject = "Rollback Test 2",
            DocumentDate = new DateTime(2026, 5, 2),
            OriginalFileNumber = "TRK-RB-001" // duplicate!
        };
        // Expect exception
        var act = () => _service.RegisterAsync(dto2);
        await act.Should().ThrowAsync<Exception>();

        // Register a different doc — should get 0002/2026, NOT 0003/2026
        // This proves the failed attempt did NOT consume a sequence number
        var dto3 = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Sender",
            Subject = "Rollback Test 3",
            DocumentDate = new DateTime(2026, 5, 3),
            OriginalFileNumber = "TRK-RB-002" // different file number
        };
        var doc3 = await _service.RegisterAsync(dto3);
        doc3.TrackingId.Should().Be("0002/2026",
            "the failed registration should not have consumed a tracking ID because the transaction was rolled back");
    }

    [Fact]
    public async Task RegisterAsync_OutgoingWithTrackingId_Works()
    {
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Outgoing,
            Recipient = "Registrar Office",
            Subject = "Outgoing Test",
            DocumentDate = new DateTime(2026, 7, 1),
            OriginalFileNumber = "TRK-OUT-001"
        };

        var result = await _service.RegisterAsync(dto);

        result.Should().NotBeNull();
        result.Direction.Should().Be(DocumentDirection.Outgoing);
        result.Recipient.Should().Be("Registrar Office");
        result.Sender.Should().BeNull();
        result.TrackingId.Should().Be("0001/2026");
    }

    [Fact]
    public async Task RegisterAsync_WithTrackingId_PreservesExistingFunctionality()
    {
        // REG-01: incoming with tracking ID still works
        var dto = new RegisterDocumentDto
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Registrar Office",
            Subject = "Admission Letter",
            DocumentDate = new DateTime(2026, 5, 29),
            OriginalFileNumber = "TRK-REG-001",
            Remarks = "Test remarks"
        };

        var result = await _service.RegisterAsync(dto);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Direction.Should().Be(DocumentDirection.Incoming);
        result.Sender.Should().Be("Registrar Office");
        result.Subject.Should().Be("Admission Letter");
        result.OriginalFileNumber.Should().Be("TRK-REG-001");
        result.Remarks.Should().Be("Test remarks");
        result.IsDeleted.Should().BeFalse();
        result.TrackingId.Should().NotBeNullOrEmpty();
        result.TrackingId.Should().EndWith("/2026");
        result.TrackingId.Should().HaveLength(9); // "0001/2026" = 9 chars
    }
}
