using FileTracker.Core.Dtos;
using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IDocumentService
{
    Task<Document> RegisterAsync(RegisterDocumentDto dto);
    Task UpdateAsync(int documentId, RegisterDocumentDto dto);
    Task<Document?> GetByIdAsync(int id);
    Task<IReadOnlyList<Document>> GetAllAsync();
    Task<SearchResultDto> SearchAsync(SearchDocumentDto filters);
}
