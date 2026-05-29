using FileTracker.Core.Models;

namespace FileTracker.Core.Services;

public interface IAttachmentService
{
    Task<Attachment> AddAttachmentAsync(int documentId, string sourceFilePath);
    Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(int documentId);
    Task RemoveAttachmentAsync(int attachmentId);
    Task OpenAttachmentAsync(int attachmentId);
}
