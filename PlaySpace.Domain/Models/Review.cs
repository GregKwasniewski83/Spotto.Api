public class Review
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Category { get; set; }
    public string Comments { get; set; }
}
