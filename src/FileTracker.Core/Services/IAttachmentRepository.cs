using System.Data;
using FileTracker.Core.Models;

namespace FileTracker.Data;

public interface IAttachmentRepository
{
    Task<int> InsertAsync(Attachment attachment, IDbTransaction? transaction = null);
    Task<IReadOnlyList<Attachment>> GetByDocumentIdAsync(int documentId);
    Task<Attachment?> GetByIdAsync(int attachmentId);
    Task DeleteAsync(int attachmentId, IDbTransaction? transaction = null);
}
