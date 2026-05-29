using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class PositionServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private PositionRepository _repository = null!;
    private PositionService _service = null!;

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
        _service = new PositionService(_repository, _connection, loggerFactory.CreateLogger<PositionService>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetAllAsync_DelegatesToRepository()
    {
        await _service.AddAsync("First");
        await _service.AddAsync("Second");

        var results = await _service.GetAllAsync();

        results.Should().HaveCount(2);
        results.Select(p => p.Name).Should().Contain(["First", "Second"]);
    }

    [Fact]
    public async Task GetActiveAsync_OnlyReturnsActivePositions()
    {
        var p1 = await _service.AddAsync("Active One");
        var p2 = await _service.AddAsync("To Deactivate");
        await _service.DeactivateAsync(p2.Id);

        var active = await _service.GetActiveAsync();

        active.Should().HaveCount(1);
        active[0].Name.Should().Be("Active One");
    }

    [Fact]
    public async Task AddAsync_AssignsSequentialDisplayOrder()
    {
        var p1 = await _service.AddAsync("First");
        var p2 = await _service.AddAsync("Second");
        var p3 = await _service.AddAsync("Third");

        p1.DisplayOrder.Should().Be(1);
        p2.DisplayOrder.Should().Be(2);
        p3.DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task AddAsync_StartsDisplayOrderAt1WhenNoExistingPositions()
    {
        var p = await _service.AddAsync("Only");

        p.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_PersistsAndReturnsNewPosition()
    {
        var result = await _service.AddAsync("Registrar");

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Registrar");
        result.IsActive.Should().BeTrue();
        result.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_ThrowsOnEmptyName()
    {
        var act = async () => await _service.AddAsync("");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public async Task AddAsync_ThrowsOnWhitespaceName()
    {
        var act = async () => await _service.AddAsync("   ");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddAsync_ThrowsOnNullName()
    {
        var act = async () => await _service.AddAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RenameAsync_UpdatesPositionName()
    {
        var p = await _service.AddAsync("Original");
        await _service.RenameAsync(p.Id, "Renamed");

        var updated = await _service.GetAllAsync();
        updated.Single(x => x.Id == p.Id).Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task RenameAsync_ThrowsOnEmptyName()
    {
        var p = await _service.AddAsync("Original");
        var act = async () => await _service.RenameAsync(p.Id, "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public async Task RenameAsync_ThrowsOnWhitespaceName()
    {
        var p = await _service.AddAsync("Original");
        var act = async () => await _service.RenameAsync(p.Id, "   ");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MoveUpAsync_SwapsDisplayOrderWithPreviousPosition()
    {
        var p1 = await _service.AddAsync("First");
        var p2 = await _service.AddAsync("Second");
        var p3 = await _service.AddAsync("Third");
        // DisplayOrders: p1=1, p2=2, p3=3

        await _service.MoveUpAsync(p2.Id);

        var positions = await _service.GetAllAsync();
        positions.Single(x => x.Id == p1.Id).DisplayOrder.Should().Be(2);
        positions.Single(x => x.Id == p2.Id).DisplayOrder.Should().Be(1);
        positions.Single(x => x.Id == p3.Id).DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task MoveDownAsync_SwapsDisplayOrderWithNextPosition()
    {
        var p1 = await _service.AddAsync("First");
        var p2 = await _service.AddAsync("Second");
        var p3 = await _service.AddAsync("Third");
        // DisplayOrders: p1=1, p2=2, p3=3

        await _service.MoveDownAsync(p2.Id);

        var positions = await _service.GetAllAsync();
        positions.Single(x => x.Id == p1.Id).DisplayOrder.Should().Be(1);
        positions.Single(x => x.Id == p2.Id).DisplayOrder.Should().Be(3);
        positions.Single(x => x.Id == p3.Id).DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task MoveDownAsync_OnLastPosition_IsNoOp()
    {
        var p1 = await _service.AddAsync("First");
        var p2 = await _service.AddAsync("Second");

        await _service.MoveDownAsync(p2.Id);

        var positions = await _service.GetAllAsync();
        positions.Single(x => x.Id == p1.Id).DisplayOrder.Should().Be(1);
        positions.Single(x => x.Id == p2.Id).DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task MoveUpAsync_OnFirstPosition_IsNoOp()
    {
        var p1 = await _service.AddAsync("First");
        var p2 = await _service.AddAsync("Second");

        await _service.MoveUpAsync(p1.Id);

        var positions = await _service.GetAllAsync();
        positions.Single(x => x.Id == p1.Id).DisplayOrder.Should().Be(1);
        positions.Single(x => x.Id == p2.Id).DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        var p = await _service.AddAsync("To Deactivate");
        await _service.DeactivateAsync(p.Id);

        var all = await _service.GetAllAsync();
        all.Single(x => x.Id == p.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_RemovesFromActiveQuery()
    {
        var p1 = await _service.AddAsync("Active");
        var p2 = await _service.AddAsync("Inactive");
        await _service.DeactivateAsync(p2.Id);

        var active = await _service.GetActiveAsync();
        active.Should().HaveCount(1);
        active[0].Id.Should().Be(p1.Id);
    }
}
