using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FileTracker.Data;

public class DatabaseInitializer
{
    private readonly SqliteConnection _db;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(SqliteConnection db, ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing database schema...");

        const string createDocumentsTable = @"
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
            );";

        const string createUniqueIndex = @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Documents_OriginalFileNumber
            ON Documents(OriginalFileNumber);";

        const string createTrackingSequence = @"
            CREATE TABLE IF NOT EXISTS TrackingSequence (
                Year INTEGER NOT NULL PRIMARY KEY,
                LastNumber INTEGER NOT NULL DEFAULT 0
            );";

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = createDocumentsTable;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = createUniqueIndex;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = createTrackingSequence;
        await cmd.ExecuteNonQueryAsync();

        const string createDocumentAudit = @"
            CREATE TABLE IF NOT EXISTS DocumentAudit (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                FieldName TEXT NOT NULL,
                OldValue TEXT,
                NewValue TEXT,
                ChangedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_DocumentAudit_DocumentId
            ON DocumentAudit(DocumentId);";

        cmd.CommandText = createDocumentAudit;
        await cmd.ExecuteNonQueryAsync();

        const string createPositionsTable = @"
            CREATE TABLE IF NOT EXISTS Positions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DisplayOrder INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE INDEX IF NOT EXISTS IX_Positions_DisplayOrder
            ON Positions(DisplayOrder);";

        cmd.CommandText = createPositionsTable;
        await cmd.ExecuteNonQueryAsync();

        // Seed default positions (D-05) — idempotent: only if table is empty
        const string seedCheck = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Faculty/Department', 1, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions LIMIT 1);";

        cmd.CommandText = seedCheck;
        await cmd.ExecuteNonQueryAsync();

        const string seedPositions = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Assistant Registrar', 2, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Assistant Registrar');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Deputy Registrar', 3, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Deputy Registrar');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Assistant Executive Engr', 4, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Assistant Executive Engr');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Executive Engineer', 5, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Executive Engineer');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Registrar', 6, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Registrar');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Dean Admin', 7, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Dean Admin');
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            SELECT 'Director', 8, 1
            WHERE NOT EXISTS (SELECT 1 FROM Positions WHERE Name = 'Director');";

        cmd.CommandText = seedPositions;
        await cmd.ExecuteNonQueryAsync();

        const string createMovementsTable = @"
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
            CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId
            ON Movements(DocumentId);
            CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId_Date
            ON Movements(DocumentId, MovementDate);";

        cmd.CommandText = createMovementsTable;
        await cmd.ExecuteNonQueryAsync();

        const string createAttachmentsTable = @"
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

        cmd.CommandText = createAttachmentsTable;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Database schema initialized successfully.");
    }
}
