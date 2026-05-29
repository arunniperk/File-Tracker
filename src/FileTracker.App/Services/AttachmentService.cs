using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;
using NotFoundException = FileTracker.Core.Exceptions.NotFoundException;

namespace FileTracker.App.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IDocumentRepository _docRepo;
    private readonly IAttachmentRepository _attachmentRepo;
    private readonly ILogger<AttachmentService> _logger;
    private readonly string _attachmentRoot;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private const long MaxFileSize = 10_485_760; // 10 MB

    private static readonly Dictionary<string, string> ExtensionMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    public AttachmentService(
        IDocumentRepository docRepo,
        IAttachmentRepository attachmentRepo,
        ILogger<AttachmentService> logger,
        string? attachmentRoot = null)
    {
        _docRepo = docRepo;
        _attachmentRepo = attachmentRepo;
        _logger = logger;
        _attachmentRoot = attachmentRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileTracker", "attachments");
    }

    public async Task<Attachment> AddAttachmentAsync(int documentId, string sourceFilePath)
    {
        // 1. Validate document exists
        var document = await _docRepo.GetByIdAsync(documentId)
            ?? throw new NotFoundException($"Document with ID {documentId} was not found.");

        // 2. Validate file extension
        var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                $"File extension '{extension}' is not allowed. Only .pdf, .jpg, .jpeg, .png files are accepted.",
                nameof(sourceFilePath));
        }

        // 3. Validate file size
        var fileInfo = new FileInfo(sourceFilePath);
        if (fileInfo.Length > MaxFileSize)
        {
            throw new ArgumentException(
                $"File size ({fileInfo.Length} bytes) exceeds the maximum allowed size of {MaxFileSize} bytes (10 MB).",
                nameof(sourceFilePath));
        }

        // 4. Construct storage path
        var documentDir = Path.Combine(_attachmentRoot, documentId.ToString());
        Directory.CreateDirectory(documentDir);

        // 5. Generate unique filename with timestamp prefix
        var safeFileName = Path.GetFileName(sourceFilePath);
        var uniqueFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeFileName}";
        var destPath = Path.Combine(documentDir, uniqueFileName);

        // 6. Copy file
        File.Copy(sourceFilePath, destPath, overwrite: false);

        // 7. Insert DB record
        var attachment = new Attachment
        {
            DocumentId = documentId,
            FileName = safeFileName, // Original filename, not the timestamped one
            StoragePath = destPath,
            FileSize = fileInfo.Length,
            ContentType = ExtensionMimeTypes.TryGetValue(extension, out var mime) ? mime : "application/octet-stream",
            CreatedAt = DateTime.Now
        };

        try
        {
            attachment.Id = await _attachmentRepo.InsertAsync(attachment);
            return attachment;
        }
        catch
        {
            // On DB failure, clean up the copied file
            try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
            throw;
        }
    }

    public async Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(int documentId)
    {
        return await _attachmentRepo.GetByDocumentIdAsync(documentId);
    }

    public async Task RemoveAttachmentAsync(int attachmentId)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(attachmentId)
            ?? throw new NotFoundException($"Attachment with ID {attachmentId} was not found.");

        // Delete the physical file if it exists
        try
        {
            if (File.Exists(attachment.StoragePath))
            {
                File.Delete(attachment.StoragePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete physical attachment file: {Path}", attachment.StoragePath);
        }

        // Delete the DB row
        await _attachmentRepo.DeleteAsync(attachmentId);
    }

    public async Task OpenAttachmentAsync(int attachmentId)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(attachmentId)
            ?? throw new NotFoundException($"Attachment with ID {attachmentId} was not found.");

        if (!File.Exists(attachment.StoragePath))
        {
            throw new NotFoundException($"Attachment file not found at path: {attachment.StoragePath}");
        }

        // Validate path is under the managed attachments directory (T-03-06 mitigation)
        if (!attachment.StoragePath.StartsWith(_attachmentRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Attachment path is outside the managed attachments directory.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = attachment.StoragePath,
            UseShellExecute = true
        });
    }
}
