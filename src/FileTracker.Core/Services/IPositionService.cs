using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IPositionService
{
    Task<IReadOnlyList<Position>> GetAllAsync();
    Task<IReadOnlyList<Position>> GetActiveAsync();
    Task<Position> AddAsync(string name);
    Task RenameAsync(int id, string name);
    Task MoveUpAsync(int id);
    Task MoveDownAsync(int id);
    Task DeactivateAsync(int id);
}
