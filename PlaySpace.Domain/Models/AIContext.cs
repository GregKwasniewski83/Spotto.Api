public class AIContext
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public List<AIEntity> Entities { get; set; }
    public string Recommendation { get; set; }
}
