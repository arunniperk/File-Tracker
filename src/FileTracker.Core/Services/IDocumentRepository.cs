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

    /// <summary>
    /// Returns count of documents pending at each officer, ordered by count descending.
    /// "Pending" = this officer is the document's current location (most recent movement).
    /// </summary>
    Task<IReadOnlyList<OfficerPendingCountDto>> GetPendingByOfficerAsync();

    /// <summary>
    /// Returns documents registered in the last N days, newest first,
    /// with CurrentLocation populated from the most recent movement.
    /// </summary>
    Task<IReadOnlyList<Document>> GetRecentAsync(int days = 7);

    /// <summary>
    /// Returns documents whose most recent movement is older than thresholdDays.
    /// Only documents that have been moved and are stalled count as overdue.
    /// Unmoved documents are excluded.
    /// </summary>
    Task<IReadOnlyList<Document>> GetOverdueAsync(int thresholdDays = 7);

    /// <summary>
    /// Returns all non-deleted documents for the specified month and year,
    /// ordered by DocumentDate ascending.
    /// </summary>
    Task<IReadOnlyList<Document>> GetByMonthAsync(int year, int month);
}
