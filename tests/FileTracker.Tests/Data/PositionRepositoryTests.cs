using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Models;
using FileTracker.Data;

namespace FileTracker.Tests.Data;

public class PositionRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private PositionRepository _repository = null!;

    private const string CreateSchema = @"
        CREATE TABLE IF NOT EXISTS Positions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            DisplayOrder INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1
        );
        CREATE INDEX IF NOT EXISTS IX_Positions_DisplayOrder
        ON Positions(DisplayOrder);";

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = _connection.CreateCommand();
        schemaCmd.CommandText = CreateSchema;
        await schemaCmd.ExecuteNonQueryAsync();

        var loggerFactory = new NullLoggerFactory();
        _repository = new PositionRepository(_connection, loggerFactory.CreateLogger<PositionRepository>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Position> SeedPosition(string name = "Test Position", int displayOrder = 1)
    {
        var position = new Position
        {
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = true
        };
        position.Id = await _repository.InsertAsync(position);
        return position;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPositionsOrderedByDisplayOrder()
    {
        await SeedPosition("Third", 3);
        await SeedPosition("First", 1);
        await SeedPosition("Second", 2);

        var positions = await _repository.GetAllAsync();

        positions.Should().HaveCount(3);
        positions[0].DisplayOrder.Should().Be(1);
        positions[0].Name.Should().Be("First");
        positions[1].DisplayOrder.Should().Be(2);
        positions[1].Name.Should().Be("Second");
        positions[2].DisplayOrder.Should().Be(3);
        positions[2].Name.Should().Be("Third");
    }

    [Fact]
    public async Task GetActiveAsync_ExcludesDeactivatedPositions()
    {
        var active = await SeedPosition("Active", 1);
        var inactive = await SeedPosition("Inactive", 2);

        await _repository.DeactivateAsync(inactive.Id);

        var activePositions = await _repository.GetActiveAsync();

        activePositions.Should().HaveCount(1);
        activePositions[0].Id.Should().Be(active.Id);
        activePositions[0].Name.Should().Be("Active");
    }

    [Fact]
    public async Task GetActiveAsync_OnlyReturnsWhereIsActiveTrue()
    {
        await SeedPosition("Active 1", 1);
        var inactive = await SeedPosition("Inactive", 2);
        await SeedPosition("Active 3", 3);

        await _repository.DeactivateAsync(inactive.Id);

        var activePositions = await _repository.GetActiveAsync();

        activePositions.Should().HaveCount(2);
        activePositions.Select(p => p.Name).Should().Contain(["Active 1", "Active 3"]);
    }

    [Fact]
    public async Task InsertAsync_PersistsAndReturnsIdGreaterThanZero()
    {
        var position = new Position
        {
            Name = "New Position",
            DisplayOrder = 5,
            IsActive = true
        };

        var id = await _repository.InsertAsync(position);

        id.Should().BeGreaterThan(0);
        var all = await _repository.GetAllAsync();
        all.Should().ContainSingle(p => p.Id == id && p.Name == "New Position");
    }

    [Fact]
    public async Task InsertAsync_PersistsAllFields()
    {
        var position = new Position
        {
            Name = "Registrar",
            DisplayOrder = 6,
            IsActive = true
        };

        var id = await _repository.InsertAsync(position);

        var all = await _repository.GetAllAsync();
        var saved = all.Single(p => p.Id == id);
        saved.Name.Should().Be("Registrar");
        saved.DisplayOrder.Should().Be(6);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalseButRowStillExists()
    {
        var position = await SeedPosition("To Deactivate", 1);

        await _repository.DeactivateAsync(position.Id);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(position.Id);
        all[0].IsActive.Should().BeFalse();
        all[0].Name.Should().Be("To Deactivate");
    }

    [Fact]
    public async Task DeactivateAsync_RemovesFromActiveQuery()
    {
        var position = await SeedPosition("Will Deactivate", 1);
        await SeedPosition("Will Stay Active", 2);

        await _repository.DeactivateAsync(position.Id);

        var active = await _repository.GetActiveAsync();
        active.Should().HaveCount(1);
        active[0].Name.Should().Be("Will Stay Active");
    }

    [Fact]
    public async Task DeactivateAsync_IsIdempotent()
    {
        var position = await SeedPosition("Idempotent Test", 1);

        await _repository.DeactivateAsync(position.Id);
        await _repository.DeactivateAsync(position.Id); // Second call should not throw

        var all = await _repository.GetAllAsync();
        all[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndDisplayOrder()
    {
        var position = await SeedPosition("Original Name", 1);

        position.Name = "Updated Name";
        position.DisplayOrder = 10;
        await _repository.UpdateAsync(position);

        var all = await _repository.GetAllAsync();
        var updated = all.Single(p => p.Id == position.Id);
        updated.Name.Should().Be("Updated Name");
        updated.DisplayOrder.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotAffectOtherPositions()
    {
        var pos1 = await SeedPosition("Position 1", 1);
        var pos2 = await SeedPosition("Position 2", 2);

        pos1.Name = "Renamed";
        await _repository.UpdateAsync(pos1);

        var all = await _repository.GetAllAsync();
        var unchanged = all.Single(p => p.Id == pos2.Id);
        unchanged.Name.Should().Be("Position 2");
        unchanged.DisplayOrder.Should().Be(2);
    }
}
