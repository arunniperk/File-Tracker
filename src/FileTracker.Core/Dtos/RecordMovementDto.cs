using FileTracker.Core.Models;

namespace FileTracker.Core.Dtos;

public class RecordMovementDto
{
    public int DocumentId { get; set; }
    public int? FromPositionId { get; set; }
    public int ToPositionId { get; set; }
    public MovementDirection Direction { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Remarks { get; set; }

    public Movement ToEntity()
    {
        return new Movement
        {
            DocumentId = DocumentId,
            FromPositionId = FromPositionId,
            ToPositionId = ToPositionId,
            Direction = Direction,
            MovementDate = MovementDate,
            Remarks = Remarks,
            CreatedAt = DateTime.UtcNow
        };
    }
}
