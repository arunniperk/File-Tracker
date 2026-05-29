using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Data;

namespace FileTracker.Core.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly SqliteConnection _db;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository repository,
        SqliteConnection db,
        ILogger<DocumentService> logger)
    {
        _repository = repository;
        _db = db;
        _logger = logger;
    }

    public async Task<Document> RegisterAsync(RegisterDocumentDto dto)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(dto.Subject))
        {
            throw new ArgumentException("Subject is required.", nameof(dto.Subject));
        }

        if (string.IsNullOrWhiteSpace(dto.OriginalFileNumber))
        {
            throw new ArgumentException("Original file number is required.", nameof(dto.OriginalFileNumber));
        }

        if (dto.DocumentDate == default)
        {
            throw new ArgumentException("Document date is required.", nameof(dto.DocumentDate));
        }

        await using var transaction = await _db.BeginTransactionAsync();
        try
        {
            // Generate tracking ID atomically within the same transaction
            var sequence = await _repository.GetNextSequenceAsync(dto.DocumentDate.Year, transaction);
            var trackingId = $"{sequence:D4}/{dto.DocumentDate.Year}";

            var document = dto.ToEntity(trackingId);
            document.Id = await _repository.InsertAsync(document, transaction);

            await transaction.CommitAsync();
            return document;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<Document?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public Task<IReadOnlyList<Document>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }
}
