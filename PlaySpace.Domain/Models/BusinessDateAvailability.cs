namespace PlaySpace.Domain.Models;

public class BusinessDateAvailability
{
    public Guid Id { get; set; }
    public Guid BusinessProfileId { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public BusinessProfile? BusinessProfile { get; set; }
}