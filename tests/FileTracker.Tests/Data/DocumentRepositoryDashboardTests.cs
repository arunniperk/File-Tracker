using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Models;
using FileTracker.Data;

namespace FileTracker.Tests.Data;

public class DocumentRepositoryDashboardTests : IAsyncLifetime
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
        CREATE TABLE IF NOT EXISTS Positions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            DisplayOrder INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS Movements (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            FromPositionId INTEGER,
            ToPositionId INTEGER NOT NULL,
            Direction TEXT NOT NULL CHECK(Direction IN ('Forward', 'Backward')),
            MovementDate TEXT NOT NULL,
            Remarks TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id),
            FOREIGN KEY (FromPositionId) REFERENCES Positions(Id),
            FOREIGN KEY (ToPositionId) REFERENCES Positions(Id)
        );
        CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId ON Movements(DocumentId);";

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

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Document> SeedDocument(string originalFileNumber, string? sender = null,
        DateTime? createdAt = null)
    {
        var doc = new Document
        {
            Direction = DocumentDirection.Incoming,
            Sender = sender ?? "Default Sender",
            Subject = "Test Subject",
            DocumentDate = DateTime.Today,
            OriginalFileNumber = originalFileNumber,
            TrackingId = "0001/2026",
            Remarks = null,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        doc.Id = await _repository.InsertAsync(doc);
        return doc;
    }

    private async Task<int> SeedPosition(string name)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Positions (Name, DisplayOrder) VALUES (@Name, 1); SELECT last_insert_rowid();";
        var param = cmd.CreateParameter();
        param.ParameterName = "@Name";
        param.Value = name;
        cmd.Parameters.Add(param);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result!);
    }

    private async Task<int> SeedMovement(int documentId, int toPositionId, DateTime movementDate)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Movements (DocumentId, ToPositionId, Direction, MovementDate, CreatedAt)
            VALUES (@DocId, @ToPosId, 'Forward', @MovementDate, @CreatedAt);
            SELECT last_insert_rowid();";

        var pDoc = cmd.CreateParameter(); pDoc.ParameterName = "@DocId"; pDoc.Value = documentId; cmd.Parameters.Add(pDoc);
        var pPos = cmd.CreateParameter(); pPos.ParameterName = "@ToPosId"; pPos.Value = toPositionId; cmd.Parameters.Add(pPos);
        var pMov = cmd.CreateParameter(); pMov.ParameterName = "@MovementDate"; pMov.Value = movementDate.ToString("yyyy-MM-dd HH:mm:ss"); cmd.Parameters.Add(pMov);
        var pCre = cmd.CreateParameter(); pCre.ParameterName = "@CreatedAt"; pCre.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"); cmd.Parameters.Add(pCre);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result!);
    }

    // ── Test 1: GetPendingByOfficerAsync returns officer counts ordered by count descending ──

    [Fact]
    public async Task GetPendingByOfficerAsync_ReturnsCountsOrderedDescending()
    {
        // Arrange: 2 positions, 3 docs, doc1&2 pending at Officer A, doc3 at Officer B
        var officerA = await SeedPosition("Officer A");
        var officerB = await SeedPosition("Officer B");

        var doc1 = await SeedDocument("PEND-001");
        var doc2 = await SeedDocument("PEND-002");
        var doc3 = await SeedDocument("PEND-003");

        await SeedMovement(doc1.Id, officerA, DateTime.UtcNow.AddDays(-1));
        await SeedMovement(doc2.Id, officerA, DateTime.UtcNow.AddDays(-2));
        await SeedMovement(doc3.Id, officerB, DateTime.UtcNow.AddDays(-3));

        // Act
        var result = await _repository.GetPendingByOfficerAsync();

        // Assert: Officer A (2) listed before Officer B (1)
        result.Should().HaveCount(2);
        result[0].OfficerName.Should().Be("Officer A");
        result[0].DocumentCount.Should().Be(2);
        result[1].OfficerName.Should().Be("Officer B");
        result[1].DocumentCount.Should().Be(1);
    }

    // ── Test 2: GetPendingByOfficerAsync returns empty list when no movements ──

    [Fact]
    public async Task GetPendingByOfficerAsync_ReturnsEmptyWhenNoMovements()
    {
        // Arrange: docs exist but no movements
        await SeedDocument("PEND-EMPTY-1");
        await SeedDocument("PEND-EMPTY-2");

        // Act
        var result = await _repository.GetPendingByOfficerAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ── Test 3: GetRecentAsync returns documents from last 7 days with CurrentLocation ──

    [Fact]
    public async Task GetRecentAsync_ReturnsRecentDocumentsWithCurrentLocation()
    {
        // Arrange: doc created 3 days ago, has been moved to a position
        var position = await SeedPosition("Registrar Desk");
        var recentDoc = await SeedDocument("REC-001", createdAt: DateTime.UtcNow.AddDays(-3));
        await SeedMovement(recentDoc.Id, position, DateTime.UtcNow.AddDays(-2));

        // Act
        var result = await _repository.GetRecentAsync(days: 7);

        // Assert
        result.Should().HaveCount(1);
        result[0].OriginalFileNumber.Should().Be("REC-001");
        result[0].CurrentLocation.Should().Be("Registrar Desk");
    }

    // ── Test 4: GetRecentAsync excludes documents older than 7 days ──

    [Fact]
    public async Task GetRecentAsync_ExcludesOldDocuments()
    {
        // Arrange
        var oldDoc = await SeedDocument("REC-OLD", createdAt: DateTime.UtcNow.AddDays(-10));
        var recentDoc = await SeedDocument("REC-NEW", createdAt: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _repository.GetRecentAsync(days: 7);

        // Assert
        result.Should().ContainSingle(d => d.OriginalFileNumber == "REC-NEW");
        result.Should().NotContain(d => d.OriginalFileNumber == "REC-OLD");
    }

    // ── Test 5: GetOverdueAsync returns stalled documents with CurrentLocation ──

    [Fact]
    public async Task GetOverdueAsync_ReturnsStalledDocuments()
    {
        // Arrange: doc1 moved 10 days ago (stalled/overdue), doc2 moved today (not overdue)
        var position = await SeedPosition("Officer X");
        var stalledDoc = await SeedDocument("OVER-001", createdAt: DateTime.UtcNow.AddDays(-15));
        await SeedMovement(stalledDoc.Id, position, DateTime.UtcNow.AddDays(-10));

        var activeDoc = await SeedDocument("OVER-002", createdAt: DateTime.UtcNow.AddDays(-5));
        await SeedMovement(activeDoc.Id, position, DateTime.UtcNow.AddDays(0));

        // Act
        var result = await _repository.GetOverdueAsync(thresholdDays: 7);

        // Assert
        result.Should().ContainSingle(d => d.OriginalFileNumber == "OVER-001");
        result[0].CurrentLocation.Should().Be("Officer X");
    }

    // ── Test 6: GetOverdueAsync returns empty when all moved recently ──

    [Fact]
    public async Task GetOverdueAsync_ReturnsEmptyWhenAllRecent()
    {
        // Arrange: all docs moved within the threshold
        var position = await SeedPosition("Officer Y");
        var doc1 = await SeedDocument("OVER-REC-1", createdAt: DateTime.UtcNow.AddDays(-5));
        await SeedMovement(doc1.Id, position, DateTime.UtcNow.AddDays(-1));

        var doc2 = await SeedDocument("OVER-REC-2", createdAt: DateTime.UtcNow.AddDays(-3));
        await SeedMovement(doc2.Id, position, DateTime.UtcNow.AddDays(-2));

        // Act
        var result = await _repository.GetOverdueAsync(thresholdDays: 7);

        // Assert
        result.Should().BeEmpty();
    }

    // ── Additional: document with NO movements is NOT overdue ──

    [Fact]
    public async Task GetOverdueAsync_ExcludesDocumentWithNoMovements()
    {
        // Arrange: doc exists but never moved — should not appear as overdue
        var position = await SeedPosition("Officer Z");
        var movedDoc = await SeedDocument("OVER-MOVED", createdAt: DateTime.UtcNow.AddDays(-15));
        await SeedMovement(movedDoc.Id, position, DateTime.UtcNow.AddDays(-12));

        var unmovedDoc = await SeedDocument("OVER-UNMOVED", createdAt: DateTime.UtcNow.AddDays(-20));

        // Act
        var result = await _repository.GetOverdueAsync(thresholdDays: 7);

        // Assert
        result.Should().ContainSingle(d => d.OriginalFileNumber == "OVER-MOVED");
        result.Should().NotContain(d => d.OriginalFileNumber == "OVER-UNMOVED");
    }

    // ── Helpers: report query seeding ────────────────────────────

    private async Task<Document> SeedDocumentWithDate(string originalFileNumber, DocumentDirection direction,
        string? sender, DateTime documentDate, string trackingId = "0001/2026")
    {
        var doc = new Document
        {
            Direction = direction,
            Sender = sender ?? "Default Sender",
            Recipient = direction == DocumentDirection.Outgoing ? "Default Recipient" : null,
            Subject = "Report Test Subject",
            DocumentDate = documentDate,
            OriginalFileNumber = originalFileNumber,
            TrackingId = trackingId,
            Remarks = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        doc.Id = await _repository.InsertAsync(doc);
        return doc;
    }

    private async Task<Document> SeedDeletedDocument(string originalFileNumber, DateTime documentDate)
    {
        // Insert normally, then update IsDeleted directly via SQL
        var doc = new Document
        {
            Direction = DocumentDirection.Incoming,
            Sender = "Some Sender",
            Subject = "Deleted Doc",
            DocumentDate = documentDate,
            OriginalFileNumber = originalFileNumber,
            TrackingId = "DEL/2026",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        doc.Id = await _repository.InsertAsync(doc);

        var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Documents SET IsDeleted = 1 WHERE Id = @Id";
        var param = cmd.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = doc.Id;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync();

        doc.IsDeleted = true;
        return doc;
    }

    // ── Test 8: GetByMonthAsync returns only documents from specified month/year, ordered by DocumentDate ──

    [Fact]
    public async Task GetByMonthAsync_ReturnsDocumentsForSpecifiedMonthOrderedByDate()
    {
        // Arrange: 3 docs in May 2026, 1 doc in April 2026
        var may1 = await SeedDocumentWithDate("REP-MAY-01", DocumentDirection.Incoming, "Dept A",
            new DateTime(2026, 5, 10));
        var may2 = await SeedDocumentWithDate("REP-MAY-02", DocumentDirection.Incoming, "Dept B",
            new DateTime(2026, 5, 15));
        var may3 = await SeedDocumentWithDate("REP-MAY-03", DocumentDirection.Outgoing, null,
            new DateTime(2026, 5, 5));
        var apr1 = await SeedDocumentWithDate("REP-APR-01", DocumentDirection.Incoming, "Dept C",
            new DateTime(2026, 4, 20));

        // Act
        var result = await _repository.GetByMonthAsync(2026, 5);

        // Assert: only May docs, ordered by DocumentDate ascending
        result.Should().HaveCount(3);
        result[0].OriginalFileNumber.Should().Be("REP-MAY-03"); // May 5
        result[1].OriginalFileNumber.Should().Be("REP-MAY-01"); // May 10
        result[2].OriginalFileNumber.Should().Be("REP-MAY-02"); // May 15
    }

    // ── Test 9: GetByMonthAsync returns empty list when no documents match ──

    [Fact]
    public async Task GetByMonthAsync_ReturnsEmptyWhenNoDocumentsMatch()
    {
        // Arrange: docs only in May 2026
        await SeedDocumentWithDate("REP-ONLY-MAY", DocumentDirection.Incoming, "Dept A",
            new DateTime(2026, 5, 10));

        // Act: query for June 2026 (no docs)
        var result = await _repository.GetByMonthAsync(2026, 6);

        // Assert
        result.Should().BeEmpty();
    }

    // ── Test 10: GetByMonthAsync excludes soft-deleted documents ──

    [Fact]
    public async Task GetByMonthAsync_ExcludesSoftDeletedDocuments()
    {
        // Arrange: 2 docs in May, one is soft-deleted
        var activeDoc = await SeedDocumentWithDate("REP-ACTIVE", DocumentDirection.Incoming, "Dept A",
            new DateTime(2026, 5, 10));
        var deletedDoc = await SeedDeletedDocument("REP-DELETED", new DateTime(2026, 5, 12));

        // Act
        var result = await _repository.GetByMonthAsync(2026, 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].OriginalFileNumber.Should().Be("REP-ACTIVE");
    }

    // ── Test 11: GetByMonthAsync returns both Incoming and Outgoing documents ──

    [Fact]
    public async Task GetByMonthAsync_ReturnsBothIncomingAndOutgoing()
    {
        // Arrange
        var incoming = await SeedDocumentWithDate("REP-IN", DocumentDirection.Incoming, "Sender X",
            new DateTime(2026, 5, 10));
        var outgoing = await SeedDocumentWithDate("REP-OUT", DocumentDirection.Outgoing, null,
            new DateTime(2026, 5, 12));

        // Act
        var result = await _repository.GetByMonthAsync(2026, 5);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Direction == DocumentDirection.Incoming);
        result.Should().Contain(d => d.Direction == DocumentDirection.Outgoing);
    }
}
