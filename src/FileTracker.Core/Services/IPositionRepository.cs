using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IPositionRepository
{
    Task<IReadOnlyList<Position>> GetAllAsync();
    Task<IReadOnlyList<Position>> GetActiveAsync();
    Task<int> InsertAsync(Position position);
    Task UpdateAsync(Position position);
    Task DeactivateAsync(int positionId);
}
