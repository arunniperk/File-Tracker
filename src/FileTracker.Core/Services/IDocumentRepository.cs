using System.Data;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;

namespace FileTracker.Data;

public interface IDocumentRepository
{
    Task<int> InsertAsync(Document document, IDbTransaction? transaction = null);
    Task UpdateAsync(Document document, IDbTransaction? transaction = null);
    Task<Document?> GetByIdAsync(int id);
    Task<IReadOnlyList<Document>> GetAllAsync();
    Task<int> GetNextSequenceAsync(int year, IDbTransaction transaction);
    Task InsertAuditEntryAsync(DocumentAudit audit, IDbTransaction? transaction = null);
    Task<IReadOnlyList<DocumentAudit>> GetAuditEntriesAsync(int documentId);
    Task<(IReadOnlyList<Document> Results, int TotalCount)> SearchAsync(SearchDocumentDto filters);
}
