namespace PlaySpace.Domain.Models
{
    public class Achievement
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Progress { get; set; }
    }
}
