using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Models;
using FileTracker.Data;

namespace FileTracker.Tests.Data;

public class DocumentRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DocumentRepository _repository = null!;

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
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Document> SeedDocument(string originalFileNumber = "REPO-TEST-001")
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
        doc.Id = await _repository.InsertAsync(doc);
        return doc;
    }

    [Fact]
    public async Task InsertAuditEntryAsync_PersistsCorrectly()
    {
        var doc = await SeedDocument("REPO-AUD-001");

        var audit = new DocumentAudit
        {
            DocumentId = doc.Id,
            FieldName = "Subject",
            OldValue = "Old Subject",
            NewValue = "New Subject",
            ChangedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditEntryAsync(audit);

        var entries = await _repository.GetAuditEntriesAsync(doc.Id);
        entries.Should().HaveCount(1);
        entries[0].FieldName.Should().Be("Subject");
        entries[0].OldValue.Should().Be("Old Subject");
        entries[0].NewValue.Should().Be("New Subject");
        entries[0].DocumentId.Should().Be(doc.Id);
    }

    [Fact]
    public async Task InsertAuditEntryAsync_WithNullOldValue_Works()
    {
        var doc = await SeedDocument("REPO-AUD-002");

        var audit = new DocumentAudit
        {
            DocumentId = doc.Id,
            FieldName = "Created",
            OldValue = null,
            NewValue = "Document registered",
            ChangedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditEntryAsync(audit);

        var entries = await _repository.GetAuditEntriesAsync(doc.Id);
        entries.Should().HaveCount(1);
        entries[0].OldValue.Should().BeNull();
        entries[0].NewValue.Should().Be("Document registered");
    }

    [Fact]
    public async Task GetAuditEntriesAsync_ReturnsNewestFirst()
    {
        var doc = await SeedDocument("REPO-AUD-003");

        var audit1 = new DocumentAudit
        {
            DocumentId = doc.Id,
            FieldName = "Subject",
            OldValue = "Old",
            NewValue = "New",
            ChangedAt = new DateTime(2026, 1, 1, 10, 0, 0)
        };
        var audit2 = new DocumentAudit
        {
            DocumentId = doc.Id,
            FieldName = "Sender",
            OldValue = "Old Sender",
            NewValue = "New Sender",
            ChangedAt = new DateTime(2026, 1, 1, 11, 0, 0)
        };

        await _repository.InsertAuditEntryAsync(audit1);
        await _repository.InsertAuditEntryAsync(audit2);

        var entries = await _repository.GetAuditEntriesAsync(doc.Id);
        entries.Should().HaveCount(2);
        entries[0].FieldName.Should().Be("Sender"); // newest first
        entries[1].FieldName.Should().Be("Subject");
    }

    [Fact]
    public async Task GetAuditEntriesAsync_ForDifferentDocuments_IsolatesCorrectly()
    {
        var doc1 = await SeedDocument("REPO-AUD-004");
        var doc2 = await SeedDocument("REPO-AUD-005");

        var audit1 = new DocumentAudit
        {
            DocumentId = doc1.Id,
            FieldName = "Subject",
            OldValue = "Old 1",
            NewValue = "New 1",
            ChangedAt = DateTime.UtcNow
        };
        var audit2 = new DocumentAudit
        {
            DocumentId = doc2.Id,
            FieldName = "Sender",
            OldValue = "Old 2",
            NewValue = "New 2",
            ChangedAt = DateTime.UtcNow
        };

        await _repository.InsertAuditEntryAsync(audit1);
        await _repository.InsertAuditEntryAsync(audit2);

        var entries1 = await _repository.GetAuditEntriesAsync(doc1.Id);
        entries1.Should().HaveCount(1);
        entries1[0].FieldName.Should().Be("Subject");

        var entries2 = await _repository.GetAuditEntriesAsync(doc2.Id);
        entries2.Should().HaveCount(1);
        entries2[0].FieldName.Should().Be("Sender");
    }

    [Fact]
    public async Task InsertAuditEntry_WithNonExistentDocumentId_ThrowsForeignKeyError()
    {
        var audit = new DocumentAudit
        {
            DocumentId = 99999,
            FieldName = "Subject",
            OldValue = "Old",
            NewValue = "New",
            ChangedAt = DateTime.UtcNow
        };

        var act = async () => await _repository.InsertAuditEntryAsync(audit);
        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAllMutableFields()
    {
        var doc = await SeedDocument("REPO-UPD-001");

        var updated = new Document
        {
            Id = doc.Id,
            Subject = "Updated Subject",
            Sender = "Updated Sender",
            Recipient = "Updated Recipient",
            OriginalFileNumber = "REPO-UPD-001",
            Remarks = "Updated Remarks",
            DocumentDate = new DateTime(2026, 6, 1),
            UpdatedAt = new DateTime(2026, 5, 30),
            CreatedAt = doc.CreatedAt
        };

        await _repository.UpdateAsync(updated);

        var result = await _repository.GetByIdAsync(doc.Id);
        result.Should().NotBeNull();
        result!.Subject.Should().Be("Updated Subject");
        result.Sender.Should().Be("Updated Sender");
        result.Remarks.Should().Be("Updated Remarks");
    }

    [Fact]
    public async Task UpdateAsync_OnlyUpdatesMutableFields()
    {
        var doc = await SeedDocument("REPO-UPD-002");
        var originalDirection = doc.Direction.ToString();

        var updated = new Document
        {
            Id = doc.Id,
            Direction = DocumentDirection.Outgoing, // attempt to change
            Subject = doc.Subject,
            Sender = "Changed Sender",
            Recipient = doc.Recipient,
            OriginalFileNumber = doc.OriginalFileNumber,
            TrackingId = doc.TrackingId,
            Remarks = doc.Remarks,
            DocumentDate = doc.DocumentDate,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = doc.CreatedAt
        };

        await _repository.UpdateAsync(updated);

        var result = await _repository.GetByIdAsync(doc.Id);
        result.Should().NotBeNull();
        result!.Sender.Should().Be("Changed Sender");
        // Direction should NOT have changed — the UPDATE SQL doesn't include it
        result.Direction.ToString().Should().Be(originalDirection);
    }
}
