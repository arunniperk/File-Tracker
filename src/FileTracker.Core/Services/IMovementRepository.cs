using System.Data;
using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IMovementRepository
{
    Task<int> InsertAsync(Movement movement, IDbTransaction? transaction = null);
    Task<IReadOnlyList<Movement>> GetByDocumentIdAsync(int documentId);
    Task<Movement?> GetCurrentLocationAsync(int documentId);
}
