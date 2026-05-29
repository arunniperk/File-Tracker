namespace FileTracker.Core.Models;

public class Attachment
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Display-only: checks whether the physical file exists on disk.
    /// </summary>
    public bool FileExists => File.Exists(StoragePath);
}
