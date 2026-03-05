namespace PlaySpace.Domain.Models
{
    public class FtpConfiguration
    {
        public string Host { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string BaseDirectory { get; set; } = "/";
        public string PublicUrlBase { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
