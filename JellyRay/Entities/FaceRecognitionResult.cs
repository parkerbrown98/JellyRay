namespace JellyRay.Entities;

public class FaceRecognitionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ItemId { get; set; }
    public long TimestampTicks { get; set; }

    public string Celebrity { get; set; } = string.Empty;
    public double Confidence { get; set; }

    public string Bbox { get; set; } = string.Empty; // Format: "left,top,width,height"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}