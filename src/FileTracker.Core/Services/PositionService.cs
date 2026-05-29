using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public class PositionService : IPositionService
{
    private readonly IPositionRepository _repository;
    private readonly SqliteConnection _db;
    private readonly ILogger<PositionService> _logger;

    public PositionService(
        IPositionRepository repository,
        SqliteConnection db,
        ILogger<PositionService> logger)
    {
        _repository = repository;
        _db = db;
        _logger = logger;
    }

    public Task<IReadOnlyList<Position>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    public Task<IReadOnlyList<Position>> GetActiveAsync()
    {
        return _repository.GetActiveAsync();
    }

    public async Task<Position> AddAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Position name is required and cannot be empty.", nameof(name));
        }

        var positions = await _repository.GetAllAsync();
        var newDisplayOrder = positions.Any() ? positions.Max(p => p.DisplayOrder) + 1 : 1;

        var position = new Position
        {
            Name = name.Trim(),
            DisplayOrder = newDisplayOrder,
            IsActive = true
        };

        position.Id = await _repository.InsertAsync(position);
        return position;
    }

    public async Task RenameAsync(int id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Position name is required and cannot be empty.", nameof(name));
        }

        var positions = await _repository.GetAllAsync();
        var position = positions.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"Position {id} not found");

        position.Name = name.Trim();
        await _repository.UpdateAsync(position);
    }

    public async Task MoveUpAsync(int id)
    {
        var positions = await _repository.GetAllAsync();
        var allList = positions.ToList();
        var index = allList.FindIndex(p => p.Id == id);

        if (index <= 0) return; // Already first, no-op

        var current = allList[index];
        var previous = allList[index - 1];

        // Swap DisplayOrder
        (current.DisplayOrder, previous.DisplayOrder) = (previous.DisplayOrder, current.DisplayOrder);

        await _repository.UpdateAsync(current);
        await _repository.UpdateAsync(previous);
    }

    public async Task MoveDownAsync(int id)
    {
        var positions = await _repository.GetAllAsync();
        var allList = positions.ToList();
        var index = allList.FindIndex(p => p.Id == id);

        if (index < 0 || index >= allList.Count - 1) return; // Already last or not found, no-op

        var current = allList[index];
        var next = allList[index + 1];

        // Swap DisplayOrder
        (current.DisplayOrder, next.DisplayOrder) = (next.DisplayOrder, current.DisplayOrder);

        await _repository.UpdateAsync(current);
        await _repository.UpdateAsync(next);
    }

    public Task DeactivateAsync(int id)
    {
        return _repository.DeactivateAsync(id);
    }
}
