using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;

namespace FileTracker.Data;

public class PositionRepository : IPositionRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<PositionRepository> _logger;

    public PositionRepository(SqliteConnection db, ILogger<PositionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Position>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Name, DisplayOrder, IsActive
            FROM Positions
            ORDER BY DisplayOrder";

        var results = await _db.QueryAsync<Position>(sql);
        return results.AsList();
    }

    public async Task<IReadOnlyList<Position>> GetActiveAsync()
    {
        const string sql = @"
            SELECT Id, Name, DisplayOrder, IsActive
            FROM Positions
            WHERE IsActive = 1
            ORDER BY DisplayOrder";

        var results = await _db.QueryAsync<Position>(sql);
        return results.AsList();
    }

    public async Task<int> InsertAsync(Position position)
    {
        const string sql = @"
            INSERT INTO Positions (Name, DisplayOrder, IsActive)
            VALUES (@Name, @DisplayOrder, @IsActive);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            position.Name,
            position.DisplayOrder,
            IsActive = position.IsActive ? 1 : 0
        });
    }

    public async Task UpdateAsync(Position position)
    {
        const string sql = @"
            UPDATE Positions SET
                Name = @Name,
                DisplayOrder = @DisplayOrder
            WHERE Id = @Id;";

        await _db.ExecuteAsync(sql, new
        {
            position.Id,
            position.Name,
            position.DisplayOrder
        });
    }

    public async Task DeactivateAsync(int positionId)
    {
        const string sql = @"
            UPDATE Positions SET IsActive = 0
            WHERE Id = @Id;";

        await _db.ExecuteAsync(sql, new { Id = positionId });
    }
}
