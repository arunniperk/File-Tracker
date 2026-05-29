using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.Data;

public class MovementRepository : IMovementRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<MovementRepository> _logger;

    public MovementRepository(SqliteConnection db, ILogger<MovementRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertAsync(Movement movement, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Movements
                (DocumentId, FromPositionId, ToPositionId, Direction, MovementDate, Remarks, CreatedAt)
            VALUES
                (@DocumentId, @FromPositionId, @ToPositionId, @Direction, @MovementDate, @Remarks, @CreatedAt);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            movement.DocumentId,
            movement.FromPositionId,
            movement.ToPositionId,
            Direction = movement.Direction.ToString(),
            MovementDate = movement.MovementDate.ToString("yyyy-MM-dd"),
            movement.Remarks,
            CreatedAt = movement.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task<IReadOnlyList<Movement>> GetByDocumentIdAsync(int documentId)
    {
        const string sql = @"
            SELECT m.Id, m.DocumentId, m.FromPositionId, m.ToPositionId,
                   m.Direction, m.MovementDate, m.Remarks, m.CreatedAt,
                   fp.Name AS FromPositionName,
                   tp.Name AS ToPositionName
            FROM Movements m
            LEFT JOIN Positions fp ON m.FromPositionId = fp.Id
            JOIN Positions tp ON m.ToPositionId = tp.Id
            WHERE m.DocumentId = @DocumentId
            ORDER BY m.MovementDate, m.Id;";

        var results = await _db.QueryAsync<Movement>(sql, new { DocumentId = documentId });
        return results.AsList();
    }

    public async Task<Movement?> GetCurrentLocationAsync(int documentId)
    {
        const string sql = @"
            SELECT m.Id, m.DocumentId, m.FromPositionId, m.ToPositionId,
                   m.Direction, m.MovementDate, m.Remarks, m.CreatedAt,
                   tp.Name AS ToPositionName
            FROM Movements m
            JOIN Positions tp ON m.ToPositionId = tp.Id
            WHERE m.DocumentId = @DocumentId
            ORDER BY m.MovementDate DESC, m.Id DESC
            LIMIT 1;";

        return await _db.QuerySingleOrDefaultAsync<Movement>(sql, new { DocumentId = documentId });
    }
}
