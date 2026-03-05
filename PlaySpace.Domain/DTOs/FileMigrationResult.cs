namespace PlaySpace.Domain.DTOs
{
    public class FileMigrationResult
    {
        public int TotalFiles { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public int SkippedFiles { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> MigratedFiles { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
        public bool LocalFilesDeleted { get; set; }
    }

    public class FileMigrationStatus
    {
        public int LocalFilesCount { get; set; }
        public int FtpFilesCount { get; set; }
        public List<string> LocalFolders { get; set; } = new List<string>();
        public Dictionary<string, int> FilesByFolder { get; set; } = new Dictionary<string, int>();
    }
}
