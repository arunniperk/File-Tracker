using FileTracker.Core.Models;

namespace FileTracker.Data;

public interface IDocumentRepository
{
    Task<int> InsertAsync(Document document);
    Task<Document?> GetByIdAsync(int id);
    Task<IReadOnlyList<Document>> GetAllAsync();
}
