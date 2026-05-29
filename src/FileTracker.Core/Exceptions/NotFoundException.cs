namespace FileTracker.Core.Exceptions;

public class NotFoundException : Exception
{
    public int DocumentId { get; }

    public NotFoundException(int documentId)
        : base($"Document with ID {documentId} was not found.")
    {
        DocumentId = documentId;
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}
