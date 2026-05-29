using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;

namespace FileTracker.Data;

public class DocumentRepository : IDocumentRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(SqliteConnection db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertAsync(Document document, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Documents
                (Direction, Sender, Recipient, Subject, DocumentDate,
                 OriginalFileNumber, TrackingId, Remarks, CreatedAt, UpdatedAt)
            VALUES
                (@Direction, @Sender, @Recipient, @Subject, @DocumentDate,
                 @OriginalFileNumber, @TrackingId, @Remarks, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            Direction = document.Direction.ToString(),
            document.Sender,
            document.Recipient,
            document.Subject,
            DocumentDate = document.DocumentDate.ToString("yyyy-MM-dd"),
            document.OriginalFileNumber,
            document.TrackingId,
            document.Remarks,
            CreatedAt = document.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = document.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task<int> GetNextSequenceAsync(int year, IDbTransaction transaction)
    {
        const string upsertSql = @"
            INSERT INTO TrackingSequence (Year, LastNumber)
            VALUES (@Year, 1)
            ON CONFLICT(Year) DO UPDATE SET LastNumber = LastNumber + 1
            RETURNING LastNumber;";

        return await _db.QuerySingleAsync<int>(upsertSql,
            new { Year = year }, transaction);
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, Direction, Sender, Recipient, Subject, DocumentDate,
                   OriginalFileNumber, TrackingId, Remarks, CreatedAt, UpdatedAt, IsDeleted
            FROM Documents
            WHERE Id = @Id AND IsDeleted = 0";

        return await _db.QuerySingleOrDefaultAsync<Document>(sql, new { Id = id });
    }

    public async Task UpdateAsync(Document document, IDbTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE Documents SET
                Subject = @Subject,
                OriginalFileNumber = @OriginalFileNumber,
                Sender = @Sender,
                Recipient = @Recipient,
                Remarks = @Remarks,
                DocumentDate = @DocumentDate,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id;";

        await _db.ExecuteAsync(sql, new
        {
            document.Id,
            document.Subject,
            document.OriginalFileNumber,
            document.Sender,
            document.Recipient,
            document.Remarks,
            DocumentDate = document.DocumentDate.ToString("yyyy-MM-dd"),
            UpdatedAt = document.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task InsertAuditEntryAsync(DocumentAudit audit, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO DocumentAudit (DocumentId, FieldName, OldValue, NewValue, ChangedAt)
            VALUES (@DocumentId, @FieldName, @OldValue, @NewValue, @ChangedAt);";

        await _db.ExecuteAsync(sql, new
        {
            audit.DocumentId,
            audit.FieldName,
            audit.OldValue,
            audit.NewValue,
            ChangedAt = audit.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task<IReadOnlyList<DocumentAudit>> GetAuditEntriesAsync(int documentId)
    {
        const string sql = @"
            SELECT Id, DocumentId, FieldName, OldValue, NewValue, ChangedAt
            FROM DocumentAudit
            WHERE DocumentId = @DocumentId
            ORDER BY ChangedAt DESC";

        var results = await _db.QueryAsync<DocumentAudit>(sql, new { DocumentId = documentId });
        return results.AsList();
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Direction, Sender, Recipient, Subject, DocumentDate,
                   OriginalFileNumber, TrackingId, Remarks, CreatedAt, UpdatedAt, IsDeleted
            FROM Documents
            WHERE IsDeleted = 0
            ORDER BY CreatedAt DESC
            LIMIT 200";

        var results = await _db.QueryAsync<Document>(sql);
        return results.AsList();
    }

    public async Task<(IReadOnlyList<Document> Results, int TotalCount)> SearchAsync(SearchDocumentDto filters)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(filters.OriginalFileNumber))
        {
            conditions.Add("d.OriginalFileNumber LIKE @FileNumber");
            parameters.Add("FileNumber", $"%{filters.OriginalFileNumber.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filters.TrackingId))
        {
            conditions.Add("d.TrackingId LIKE @TrackingId");
            parameters.Add("TrackingId", $"%{filters.TrackingId.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filters.Subject))
        {
            conditions.Add("d.Subject LIKE @Subject");
            parameters.Add("Subject", $"%{filters.Subject.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filters.SenderOrRecipient))
        {
            conditions.Add("(d.Sender LIKE @SenderOrRec OR d.Recipient LIKE @SenderOrRec)");
            parameters.Add("SenderOrRec", $"%{filters.SenderOrRecipient.Trim()}%");
        }

        if (filters.FromDate.HasValue)
        {
            conditions.Add("d.DocumentDate >= @FromDate");
            parameters.Add("FromDate", filters.FromDate.Value.ToString("yyyy-MM-dd"));
        }

        if (filters.ToDate.HasValue)
        {
            conditions.Add("d.DocumentDate <= @ToDate");
            parameters.Add("ToDate", filters.ToDate.Value.ToString("yyyy-MM-dd"));
        }

        var whereClause = conditions.Count > 0
            ? "WHERE d.IsDeleted = 0 AND " + string.Join(" AND ", conditions)
            : "WHERE d.IsDeleted = 0";

        var dataSql = $@"
            SELECT d.* FROM Documents d
            {whereClause}
            ORDER BY d.CreatedAt DESC
            LIMIT @PageSize OFFSET @Offset;";

        var countSql = $"SELECT COUNT(*) FROM Documents d {whereClause};";

        parameters.Add("PageSize", filters.PageSize);
        parameters.Add("Offset", (filters.Page - 1) * filters.PageSize);

        var results = await _db.QueryAsync<Document>(dataSql, parameters);
        var totalCount = await _db.QuerySingleAsync<int>(countSql, parameters);

        return (results.AsList(), totalCount);
    }

    public async Task<IReadOnlyList<OfficerPendingCountDto>> GetPendingByOfficerAsync()
    {
        const string sql = @"
            SELECT tp.Name AS OfficerName, COUNT(*) AS DocumentCount
            FROM Movements m
            JOIN Positions tp ON m.ToPositionId = tp.Id
            WHERE m.Id IN (
                SELECT MAX(m2.Id) FROM Movements m2 GROUP BY m2.DocumentId
            )
            GROUP BY m.ToPositionId
            ORDER BY DocumentCount DESC;";

        var results = await _db.QueryAsync<OfficerPendingCountDto>(sql);
        return results.AsList();
    }

    public async Task<IReadOnlyList<Document>> GetRecentAsync(int days = 7)
    {
        var dateFilter = $"-{days} days";
        var sql = $@"
            SELECT d.Id, d.Direction, d.Sender, d.Recipient, d.Subject, d.DocumentDate,
                   d.OriginalFileNumber, d.TrackingId, d.Remarks, d.CreatedAt, d.UpdatedAt, d.IsDeleted,
                   COALESCE(tp.Name, '\u2014') AS CurrentLocation
            FROM Documents d
            LEFT JOIN (
                SELECT DocumentId, ToPositionId,
                       ROW_NUMBER() OVER (PARTITION BY DocumentId ORDER BY MovementDate DESC, Id DESC) AS rn
                FROM Movements
            ) latest ON d.Id = latest.DocumentId AND latest.rn = 1
            LEFT JOIN Positions tp ON latest.ToPositionId = tp.Id
            WHERE d.IsDeleted = 0 AND d.CreatedAt >= datetime('now', @DateFilter)
            ORDER BY d.CreatedAt DESC;";

        var results = await _db.QueryAsync<Document>(sql, new { DateFilter = dateFilter });
        return results.AsList();
    }

    public async Task<IReadOnlyList<Document>> GetOverdueAsync(int thresholdDays = 7)
    {
        var dateFilter = $"-{thresholdDays} days";
        var sql = $@"
            SELECT d.Id, d.Direction, d.Sender, d.Recipient, d.Subject, d.DocumentDate,
                   d.OriginalFileNumber, d.TrackingId, d.Remarks, d.CreatedAt, d.UpdatedAt, d.IsDeleted,
                   COALESCE(tp.Name, '\u2014') AS CurrentLocation
            FROM Documents d
            INNER JOIN (
                SELECT DocumentId, ToPositionId, MovementDate,
                       ROW_NUMBER() OVER (PARTITION BY DocumentId ORDER BY MovementDate DESC, Id DESC) AS rn
                FROM Movements
            ) latest ON d.Id = latest.DocumentId AND latest.rn = 1
            LEFT JOIN Positions tp ON latest.ToPositionId = tp.Id
            WHERE d.IsDeleted = 0
              AND latest.MovementDate < datetime('now', @DateFilter)
            ORDER BY d.CreatedAt DESC;";

        var results = await _db.QueryAsync<Document>(sql, new { DateFilter = dateFilter });
        return results.AsList();
    }
}
