public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Channel { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; }
}
