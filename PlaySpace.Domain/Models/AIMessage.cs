public class AIMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; }
    public string Intent { get; set; }
    public AIContext Context { get; set; }
}
