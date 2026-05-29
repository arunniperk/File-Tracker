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

        _logger.LogInformation("Database schema initialized successfully.");
    }
}
