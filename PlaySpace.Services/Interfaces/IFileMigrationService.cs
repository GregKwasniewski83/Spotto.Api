using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces
{
    public interface IFileMigrationService
    {
        /// <summary>
        /// Migrate all existing files from local wwwroot/uploads to FTP server
        /// </summary>
        /// <param name="deleteLocalFiles">Whether to delete local files after successful migration</param>
        /// <returns>Migration result with statistics</returns>
        Task<FileMigrationResult> MigrateLocalFilesToFtpAsync(bool deleteLocalFiles = false);

        /// <summary>
        /// Get current status of local and FTP files
        /// </summary>
        /// <returns>File migration status</returns>
        Task<FileMigrationStatus> GetMigrationStatusAsync();
    }
}
