using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
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
}
