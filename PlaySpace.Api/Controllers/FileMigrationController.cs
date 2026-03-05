using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileMigrationController : ControllerBase
    {
        private readonly IFileMigrationService _fileMigrationService;
        private readonly ILogger<FileMigrationController> _logger;

        public FileMigrationController(IFileMigrationService fileMigrationService, ILogger<FileMigrationController> logger)
        {
            _fileMigrationService = fileMigrationService;
            _logger = logger;
        }

        /// <summary>
        /// Get current migration status (local vs FTP files)
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetMigrationStatus()
        {
            try
            {
                var status = await _fileMigrationService.GetMigrationStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                return StatusCode(500, new { error = "Failed to get migration status", details = ex.Message });
            }
        }

        /// <summary>
        /// Migrate all local files from wwwroot/uploads to FTP server
        /// </summary>
        /// <param name="deleteLocalFiles">Whether to delete local files after successful migration (default: false)</param>
        [HttpPost("migrate")]
        public async Task<IActionResult> MigrateFilesToFtp([FromQuery] bool deleteLocalFiles = false)
        {
            try
            {
                _logger.LogInformation("Migration requested. Delete local files: {DeleteLocalFiles}", deleteLocalFiles);

                var result = await _fileMigrationService.MigrateLocalFilesToFtpAsync(deleteLocalFiles);

                if (result.FailedMigrations > 0)
                {
                    _logger.LogWarning("Migration completed with {FailedCount} failures", result.FailedMigrations);
                    return Ok(new
                    {
                        success = true,
                        message = $"Migration completed with {result.FailedMigrations} failures",
                        result
                    });
                }

                _logger.LogInformation("Migration completed successfully. {SuccessCount} files migrated", result.SuccessfulMigrations);
                return Ok(new
                {
                    success = true,
                    message = "All files migrated successfully",
                    result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during file migration");
                return StatusCode(500, new { error = "Migration failed", details = ex.Message });
            }
        }
    }
}
