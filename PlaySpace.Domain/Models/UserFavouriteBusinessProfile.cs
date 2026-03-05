namespace PlaySpace.Domain.Models;

public class UserFavouriteBusinessProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BusinessProfileId { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
}
