using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class DocumentServiceSearchTests : IAsyncLifetime
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
        );
        CREATE TABLE IF NOT EXISTS DocumentAudit (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            FieldName TEXT NOT NULL,
            OldValue TEXT,
            NewValue TEXT,
            ChangedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
        );
        CREATE INDEX IF NOT EXISTS IX_DocumentAudit_DocumentId ON DocumentAudit(DocumentId);";

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

    private async Task<Document> SeedDocument(string fileNum, string subject, string? sender, string? recipient, string? trackingId, DateTime date)
    {
        var dto = new RegisterDocumentDto
        {
            Direction = sender is not null ? DocumentDirection.Incoming : DocumentDirection.Outgoing,
            Sender = sender,
            Recipient = recipient,
            Subject = subject,
            DocumentDate = date,
            OriginalFileNumber = fileNum
        };
        return await _service.RegisterAsync(dto);
    }

    [Fact]
    public async Task SearchAsync_ByOriginalFileNumber_PartialMatchReturnsCorrectDocument()
    {
        await SeedDocument("REG/2026/001", "Admission Letter", "Registrar", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Fee Receipt", "Accounts", null, null, new DateTime(2026, 2, 20));

        var filter = new SearchDocumentDto { OriginalFileNumber = "001" };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(1);
        result.Results[0].OriginalFileNumber.Should().Be("REG/2026/001");
    }

    [Fact]
    public async Task SearchAsync_ByTrackingId_PartialMatchWorks()
    {
        await SeedDocument("REG/2026/001", "First Doc", "Sender A", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Second Doc", "Sender B", null, null, new DateTime(2026, 2, 20));

        var allDocs = await _service.GetAllAsync();
        var doc2TrackingId = allDocs.First(d => d.OriginalFileNumber == "REG/2026/002").TrackingId;

        var filter = new SearchDocumentDto { TrackingId = doc2TrackingId };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(1);
        result.Results[0].OriginalFileNumber.Should().Be("REG/2026/002");
    }

    [Fact]
    public async Task SearchAsync_BySubject_PartialMatchWorks()
    {
        await SeedDocument("REG/2026/001", "Admission Letter", "Registrar", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Fee Payment Receipt", "Accounts", null, null, new DateTime(2026, 2, 20));

        var filter = new SearchDocumentDto { Subject = "Fee" };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(1);
        result.Results[0].Subject.Should().Be("Fee Payment Receipt");
    }

    [Fact]
    public async Task SearchAsync_BySenderOrRecipient_MatchesBothFields()
    {
        await SeedDocument("REG/2026/001", "Doc 1", "Alice Johnson", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Doc 2", null, "Alice Brown", null, new DateTime(2026, 2, 20));
        await SeedDocument("REG/2026/003", "Doc 3", "Bob Smith", null, null, new DateTime(2026, 3, 10));

        var filter = new SearchDocumentDto { SenderOrRecipient = "Alice" };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.OriginalFileNumber).Should().Contain(
            new[] { "REG/2026/001", "REG/2026/002" });
    }

    [Fact]
    public async Task SearchAsync_ByDateRange_InclusiveBoundaries()
    {
        await SeedDocument("REG/2026/001", "Jan Doc", "Sender", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Mar Doc", "Sender", null, null, new DateTime(2026, 3, 1));
        await SeedDocument("REG/2026/003", "Mar End Doc", "Sender", null, null, new DateTime(2026, 3, 31));
        await SeedDocument("REG/2026/004", "Apr Doc", "Sender", null, null, new DateTime(2026, 4, 10));

        var filter = new SearchDocumentDto
        {
            FromDate = new DateTime(2026, 3, 1),
            ToDate = new DateTime(2026, 3, 31)
        };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.Subject).Should().Contain(new[] { "Mar Doc", "Mar End Doc" });
    }

    [Fact]
    public async Task SearchAsync_MultipleFilters_AND_CombinedNarrowsResults()
    {
        await SeedDocument("REG/2026/001", "Fee Receipt", "Registrar", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Fee Receipt", "Accounts", null, null, new DateTime(2026, 1, 20));
        await SeedDocument("REG/2026/003", "Admission", "Registrar", null, null, new DateTime(2026, 2, 10));

        var filter = new SearchDocumentDto
        {
            Subject = "Fee",
            SenderOrRecipient = "Registrar"
        };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(1);
        result.Results[0].OriginalFileNumber.Should().Be("REG/2026/001");
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllNonDeletedDocuments_Paginated()
    {
        await SeedDocument("REG/2026/001", "Doc 1", "Sender", null, null, new DateTime(2026, 1, 15));
        await SeedDocument("REG/2026/002", "Doc 2", "Sender", null, null, new DateTime(2026, 2, 20));
        await SeedDocument("REG/2026/003", "Doc 3", "Sender", null, null, new DateTime(2026, 3, 10));

        var filter = new SearchDocumentDto(); // No filters
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_Pagination_Page2ReturnsNextResults()
    {
        for (int i = 1; i <= 5; i++)
        {
            await SeedDocument($"REG/2026/00{i}", $"Doc {i}", "Sender", null, null, new DateTime(2026, i, 1));
        }

        var filter = new SearchDocumentDto { Page = 1, PageSize = 3 };
        var page1 = await _service.SearchAsync(filter);
        page1.Results.Should().HaveCount(3);
        page1.TotalCount.Should().Be(5);

        filter.Page = 2;
        var page2 = await _service.SearchAsync(filter);
        page2.Results.Should().HaveCount(2);
        page2.TotalCount.Should().Be(5);

        // Ensure no overlap between pages
        var page1Ids = page1.Results.Select(r => r.Id).ToList();
        var page2Ids = page2.Results.Select(r => r.Id).ToList();
        page1Ids.Intersect(page2Ids).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TotalCount_ReflectsAllMatching_NotJustCurrentPage()
    {
        for (int i = 1; i <= 10; i++)
        {
            await SeedDocument($"REG/2026/{i:D2}", $"Doc {i}", "Sender", null, null, new DateTime(2026, i % 12 + 1, 1));
        }

        var filter = new SearchDocumentDto { Page = 1, PageSize = 3 };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_NoMatches()
    {
        await SeedDocument("REG/2026/001", "Admission", "Registrar", null, null, new DateTime(2026, 1, 15));

        var filter = new SearchDocumentDto { Subject = "NonexistentKeyword" };
        var result = await _service.SearchAsync(filter);

        result.Results.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_ExcludesSoftDeleted_DocumentsNeverAppear()
    {
        await SeedDocument("REG/2026/001", "Visible Doc", "Sender", null, null, new DateTime(2026, 1, 15));

        // Directly soft-delete a document via the connection
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Documents SET IsDeleted = 1 WHERE OriginalFileNumber = 'REG/2026/001'";
        await cmd.ExecuteNonQueryAsync();

        await SeedDocument("REG/2026/002", "Also Visible", "Sender", null, null, new DateTime(2026, 2, 20));

        var filter = new SearchDocumentDto();
        var result = await _service.SearchAsync(filter);

        result.Results.Should().HaveCount(1);
        result.Results[0].OriginalFileNumber.Should().Be("REG/2026/002");
    }

    [Fact]
    public async Task SearchAsync_PageSizeClamped_Exceeds100GetsClampedTo100()
    {
        await SeedDocument("REG/2026/001", "Test", "Sender", null, null, new DateTime(2026, 1, 1));

        var filter = new SearchDocumentDto { PageSize = 200 };
        var result = await _service.SearchAsync(filter);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task SearchAsync_NegativePage_ClampedTo1()
    {
        await SeedDocument("REG/2026/001", "Test", "Sender", null, null, new DateTime(2026, 1, 1));

        var filter = new SearchDocumentDto { Page = -5 };
        var result = await _service.SearchAsync(filter);

        result.Page.Should().Be(1);
        result.Results.Should().HaveCount(1);
    }
}
