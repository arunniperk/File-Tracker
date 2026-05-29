using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Data;

public class MovementRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private MovementRepository _repository = null!;
    private int _documentId;
    private int _position1Id;
    private int _position2Id;
    private int _position3Id;

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
            Direction TEXT NOT NULL CHECK(Direction IN ('Sent', 'Received')),
            MovementDate TEXT NOT NULL,
            Remarks TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id),
            FOREIGN KEY (FromPositionId) REFERENCES Positions(Id),
            FOREIGN KEY (ToPositionId) REFERENCES Positions(Id)
        );
        CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId ON Movements(DocumentId);
        CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId_Date ON Movements(DocumentId, MovementDate);";

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

        // Seed test data: 1 document + 3 positions
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = @"
            INSERT INTO Documents (Direction, Subject, DocumentDate, OriginalFileNumber, CreatedAt, UpdatedAt)
            VALUES ('Incoming', 'Test Document', '2026-05-01', 'MOV-DOC-001', '2026-05-01 10:00:00', '2026-05-01 10:00:00');
            SELECT last_insert_rowid();";
        _documentId = Convert.ToInt32(await seedCmd.ExecuteScalarAsync());

        await using var posCmd = _connection.CreateCommand();
        posCmd.CommandText = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive) VALUES ('Faculty/Department', 1, 1);
            SELECT last_insert_rowid();";
        _position1Id = Convert.ToInt32(await posCmd.ExecuteScalarAsync());

        posCmd.CommandText = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive) VALUES ('Registrar', 2, 1);
            SELECT last_insert_rowid();";
        _position2Id = Convert.ToInt32(await posCmd.ExecuteScalarAsync());

        posCmd.CommandText = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive) VALUES ('Director', 3, 1);
            SELECT last_insert_rowid();";
        _position3Id = Convert.ToInt32(await posCmd.ExecuteScalarAsync());

        var loggerFactory = new NullLoggerFactory();
        _repository = new MovementRepository(_connection, loggerFactory.CreateLogger<MovementRepository>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    private Movement CreateMovement(int? fromPositionId, int toPositionId, MovementDirection direction,
        string date = "2026-05-15", string remarks = "")
    {
        return new Movement
        {
            DocumentId = _documentId,
            FromPositionId = fromPositionId,
            ToPositionId = toPositionId,
            Direction = direction,
            MovementDate = DateTime.Parse(date),
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task InsertAsync_PersistsMovementAndReturnsId()
    {
        var movement = CreateMovement(null, _position1Id, MovementDirection.Sent, "2026-05-15", "Forwarded to Faculty");

        var id = await _repository.InsertAsync(movement);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InsertAsync_WithNullFromPositionId_Works()
    {
        var movement = CreateMovement(null, _position2Id, MovementDirection.Sent, "2026-05-15");

        var id = await _repository.InsertAsync(movement);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByDocumentIdAsync_ReturnsMovementsInChronologicalOrder()
    {
        await _repository.InsertAsync(CreateMovement(null, _position1Id, MovementDirection.Sent, "2026-05-15", "First"));
        await _repository.InsertAsync(CreateMovement(_position1Id, _position2Id, MovementDirection.Sent, "2026-05-16", "Second"));
        await _repository.InsertAsync(CreateMovement(_position2Id, _position3Id, MovementDirection.Sent, "2026-05-17", "Third"));

        var results = await _repository.GetByDocumentIdAsync(_documentId);

        results.Should().HaveCount(3);
        results[0].Remarks.Should().Be("First");
        results[1].Remarks.Should().Be("Second");
        results[2].Remarks.Should().Be("Third");
    }

    [Fact]
    public async Task GetByDocumentIdAsync_JoinsPositionNamesCorrectly()
    {
        await _repository.InsertAsync(CreateMovement(null, _position1Id, MovementDirection.Sent, "2026-05-15"));
        await _repository.InsertAsync(CreateMovement(_position1Id, _position2Id, MovementDirection.Sent, "2026-05-16"));

        var results = await _repository.GetByDocumentIdAsync(_documentId);

        results[0].FromPositionName.Should().BeNull();
        results[0].ToPositionName.Should().Be("Faculty/Department");
        results[1].FromPositionName.Should().Be("Faculty/Department");
        results[1].ToPositionName.Should().Be("Registrar");
    }

    [Fact]
    public async Task GetCurrentLocationAsync_ReturnsMostRecentMovementToPositionName()
    {
        await _repository.InsertAsync(CreateMovement(null, _position1Id, MovementDirection.Sent, "2026-05-15"));
        await _repository.InsertAsync(CreateMovement(_position1Id, _position2Id, MovementDirection.Sent, "2026-05-16"));

        var location = await _repository.GetCurrentLocationAsync(_documentId);

        location.Should().NotBeNull();
        location!.ToPositionName.Should().Be("Registrar");
    }

    [Fact]
    public async Task GetCurrentLocationAsync_ReturnsNullForDocumentWithNoMovements()
    {
        var location = await _repository.GetCurrentLocationAsync(99999);

        location.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_InvalidDocumentId_ThrowsForeignKeyError()
    {
        var movement = new Movement
        {
            DocumentId = 99999,
            ToPositionId = _position1Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today,
            CreatedAt = DateTime.UtcNow
        };

        var act = async () => await _repository.InsertAsync(movement);
        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task InsertAsync_InvalidToPositionId_ThrowsForeignKeyError()
    {
        var movement = new Movement
        {
            DocumentId = _documentId,
            ToPositionId = 99999,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today,
            CreatedAt = DateTime.UtcNow
        };

        var act = async () => await _repository.InsertAsync(movement);
        await act.Should().ThrowAsync<SqliteException>();
    }
}
