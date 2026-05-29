using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;

namespace FileTracker.Data;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<AttachmentRepository> _logger;

    public AttachmentRepository(SqliteConnection db, ILogger<AttachmentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertAsync(Attachment attachment, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Attachments
                (DocumentId, FileName, StoragePath, FileSize, ContentType, CreatedAt)
            VALUES
                (@DocumentId, @FileName, @StoragePath, @FileSize, @ContentType, @CreatedAt);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            attachment.DocumentId,
            attachment.FileName,
            attachment.StoragePath,
            attachment.FileSize,
            attachment.ContentType,
            CreatedAt = attachment.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task<IReadOnlyList<Attachment>> GetByDocumentIdAsync(int documentId)
    {
        const string sql = @"
            SELECT Id, DocumentId, FileName, StoragePath, FileSize, ContentType, CreatedAt
            FROM Attachments
            WHERE DocumentId = @DocumentId
            ORDER BY CreatedAt DESC";

        var results = await _db.QueryAsync<Attachment>(sql, new { DocumentId = documentId });
        return results.AsList();
    }

    public async Task<Attachment?> GetByIdAsync(int attachmentId)
    {
        const string sql = @"
            SELECT Id, DocumentId, FileName, StoragePath, FileSize, ContentType, CreatedAt
            FROM Attachments
            WHERE Id = @Id";

        return await _db.QuerySingleOrDefaultAsync<Attachment>(sql, new { Id = attachmentId });
    }

    public async Task DeleteAsync(int attachmentId, IDbTransaction? transaction = null)
    {
        const string sql = "DELETE FROM Attachments WHERE Id = @Id";
        await _db.ExecuteAsync(sql, new { Id = attachmentId }, transaction);
    }
}
