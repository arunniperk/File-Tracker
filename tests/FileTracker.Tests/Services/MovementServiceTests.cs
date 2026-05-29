using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class MovementServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private MovementRepository _movementRepository = null!;
    private PositionRepository _positionRepository = null!;
    private PositionService _positionService = null!;
    private MovementService _service = null!;
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

        // Seed test data
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = @"
            INSERT INTO Documents (Direction, Subject, DocumentDate, OriginalFileNumber, CreatedAt, UpdatedAt)
            VALUES ('Incoming', 'Test Document', '2026-05-01', 'MOV-SVC-001', '2026-05-01 10:00:00', '2026-05-01 10:00:00');
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
            INSERT INTO Positions (Name, DisplayOrder, IsActive) VALUES ('Director', 3, 0);
            SELECT last_insert_rowid();";
        _position3Id = Convert.ToInt32(await posCmd.ExecuteScalarAsync());

        var loggerFactory = new NullLoggerFactory();
        _positionRepository = new PositionRepository(_connection, loggerFactory.CreateLogger<PositionRepository>());
        _positionService = new PositionService(_positionRepository, _connection, loggerFactory.CreateLogger<PositionService>());
        _movementRepository = new MovementRepository(_connection, loggerFactory.CreateLogger<MovementRepository>());
        _service = new MovementService(_movementRepository, _positionService, loggerFactory.CreateLogger<MovementService>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RecordMovementAsync_StoresAllFieldsCorrectly()
    {
        var dto = new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = null,
            ToPositionId = _position1Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today,
            Remarks = "Forwarded to Faculty"
        };

        var result = await _service.RecordMovementAsync(dto);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.DocumentId.Should().Be(_documentId);
        result.FromPositionId.Should().BeNull();
        result.ToPositionId.Should().Be(_position1Id);
        result.Direction.Should().Be(MovementDirection.Sent);
        result.Remarks.Should().Be("Forwarded to Faculty");
    }

    [Fact]
    public async Task RecordMovementAsync_AllowsNullFromPositionId()
    {
        var dto = new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = null,
            ToPositionId = _position2Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today
        };

        var result = await _service.RecordMovementAsync(dto);

        result.Should().NotBeNull();
        result.FromPositionId.Should().BeNull();
    }

    [Fact]
    public async Task RecordMovementAsync_ValidatesToPositionIdIsRequired()
    {
        var dto = new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = null,
            ToPositionId = 99999, // non-existent position
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today
        };

        var act = async () => await _service.RecordMovementAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ToPositionId*");
    }

    [Fact]
    public async Task GetCurrentLocationAsync_ReturnsCorrectPositionAfterMultipleMovements()
    {
        await _service.RecordMovementAsync(new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = null,
            ToPositionId = _position1Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today.AddDays(-3)
        });

        await _service.RecordMovementAsync(new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = _position1Id,
            ToPositionId = _position2Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today
        });

        var location = await _service.GetCurrentLocationAsync(_documentId);

        location.Should().NotBeNull();
        location!.ToPositionName.Should().Be("Registrar");
    }

    [Fact]
    public async Task GetMovementHistoryAsync_ReturnsAllMovementsForDocument()
    {
        await _service.RecordMovementAsync(new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = null,
            ToPositionId = _position1Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today.AddDays(-3),
            Remarks = "First movement"
        });

        await _service.RecordMovementAsync(new RecordMovementDto
        {
            DocumentId = _documentId,
            FromPositionId = _position1Id,
            ToPositionId = _position2Id,
            Direction = MovementDirection.Sent,
            MovementDate = DateTime.Today,
            Remarks = "Second movement"
        });

        var history = await _service.GetMovementHistoryAsync(_documentId);

        history.Should().HaveCount(2);
        history[0].Remarks.Should().Be("First movement");
        history[1].Remarks.Should().Be("Second movement");
    }

    [Fact]
    public async Task GetMovementHistoryAsync_ReturnsEmptyListForDocumentWithNoMovements()
    {
        var history = await _service.GetMovementHistoryAsync(99999);

        history.Should().NotBeNull();
        history.Should().BeEmpty();
    }
}
