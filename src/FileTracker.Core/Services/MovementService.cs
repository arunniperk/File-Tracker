using Microsoft.Extensions.Logging;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public class MovementService : IMovementService
{
    private readonly IMovementRepository _repository;
    private readonly IPositionService _positionService;
    private readonly ILogger<MovementService> _logger;

    public MovementService(
        IMovementRepository repository,
        IPositionService positionService,
        ILogger<MovementService> logger)
    {
        _repository = repository;
        _positionService = positionService;
        _logger = logger;
    }

    public async Task<Movement> RecordMovementAsync(RecordMovementDto dto)
    {
        // Validate ToPositionId references an existing active position
        var activePositions = await _positionService.GetActiveAsync();
        if (!activePositions.Any(p => p.Id == dto.ToPositionId))
        {
            throw new ArgumentException(
                $"ToPositionId {dto.ToPositionId} does not reference an active position.",
                nameof(dto.ToPositionId));
        }

        // If FromPositionId is provided, validate it references an existing position (active or inactive)
        if (dto.FromPositionId.HasValue)
        {
            var allPositions = await _positionService.GetAllAsync();
            if (!allPositions.Any(p => p.Id == dto.FromPositionId.Value))
            {
                throw new ArgumentException(
                    $"FromPositionId {dto.FromPositionId} does not reference an existing position.",
                    nameof(dto.FromPositionId));
            }
        }

        var movement = dto.ToEntity();
        movement.Id = await _repository.InsertAsync(movement);
        return movement;
    }

    public Task<IReadOnlyList<Movement>> GetMovementHistoryAsync(int documentId)
    {
        return _repository.GetByDocumentIdAsync(documentId);
    }

    public Task<Movement?> GetCurrentLocationAsync(int documentId)
    {
        return _repository.GetCurrentLocationAsync(documentId);
    }
}
