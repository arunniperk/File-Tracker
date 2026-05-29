using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FileTracker.Data;

public class DocumentRepository : IDocumentRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(SqliteConnection db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }
}
