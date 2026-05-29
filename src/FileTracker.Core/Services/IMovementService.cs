using FileTracker.Core.Dtos;
using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IMovementService
{
    Task<Movement> RecordMovementAsync(RecordMovementDto dto);
    Task<IReadOnlyList<Movement>> GetMovementHistoryAsync(int documentId);
    Task<Movement?> GetCurrentLocationAsync(int documentId);
}
