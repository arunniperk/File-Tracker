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
        ON Documents(OriginalFileNumber);";

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
}
