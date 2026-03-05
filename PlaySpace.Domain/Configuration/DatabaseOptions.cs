namespace PlaySpace.Domain.Configuration
{
    public class DatabaseOptions
    {
        public const string Key = "ConnectionStrings";
        
        public string DefaultConnection { get; set; } = string.Empty;
    }
}