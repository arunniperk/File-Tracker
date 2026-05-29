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

            // Insert initial audit entry for document creation
            var createAudit = new DocumentAudit
            {
                DocumentId = document.Id,
                FieldName = "Created",
                OldValue = null,
                NewValue = "Document registered",
                ChangedAt = DateTime.UtcNow
            };
            await _repository.InsertAuditEntryAsync(createAudit, transaction);

            await transaction.CommitAsync();
            return document;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(int documentId, RegisterDocumentDto dto)
    {
        var existing = await _repository.GetByIdAsync(documentId)
            ?? throw new NotFoundException($"Document {documentId} not found");

        var audits = new List<DocumentAudit>();
        var now = DateTime.UtcNow;

        void CheckAndAudit(string fieldName, string? oldVal, string? newVal)
        {
            if (oldVal != newVal)
            {
                audits.Add(new DocumentAudit
                {
                    DocumentId = documentId,
                    FieldName = fieldName,
                    OldValue = oldVal,
                    NewValue = newVal,
                    ChangedAt = now
                });
            }
        }

        // Direction is NOT editable after creation — exclude from diff
        CheckAndAudit("Sender", existing.Sender, dto.Sender);
        CheckAndAudit("Recipient", existing.Recipient, dto.Recipient);
        CheckAndAudit("Subject", existing.Subject, dto.Subject);
        CheckAndAudit("OriginalFileNumber", existing.OriginalFileNumber, dto.OriginalFileNumber);
        CheckAndAudit("Remarks", existing.Remarks, dto.Remarks);
        CheckAndAudit("DocumentDate",
            existing.DocumentDate.ToString("yyyy-MM-dd"),
            dto.DocumentDate.ToString("yyyy-MM-dd"));

        if (audits.Count == 0)
        {
            // No changes — don't write to DB
            return;
        }

        // Apply changes to existing entity
        existing.Sender = dto.Sender;
        existing.Recipient = dto.Recipient;
        existing.Subject = dto.Subject;
        existing.OriginalFileNumber = dto.OriginalFileNumber;
        existing.Remarks = dto.Remarks;
        existing.DocumentDate = dto.DocumentDate;
        existing.UpdatedAt = now;
        // Direction and TrackingId are NOT updated — immutable after creation

        await using var transaction = await _db.BeginTransactionAsync();
        try
        {
            await _repository.UpdateAsync(existing, transaction);
            foreach (var audit in audits)
            {
                await _repository.InsertAuditEntryAsync(audit, transaction);
            }
            await transaction.CommitAsync();
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

    public async Task<SearchResultDto> SearchAsync(SearchDocumentDto filters)
    {
        // Clamp Page to >= 1
        if (filters.Page < 1)
        {
            filters.Page = 1;
        }

        // Clamp PageSize to 1..100
        if (filters.PageSize < 1)
        {
            filters.PageSize = 20;
        }
        else if (filters.PageSize > 100)
        {
            filters.PageSize = 100;
        }

        var (results, totalCount) = await _repository.SearchAsync(filters);

        return new SearchResultDto
        {
            Results = results,
            TotalCount = totalCount,
            Page = filters.Page,
            PageSize = filters.PageSize
        };
    }
}
