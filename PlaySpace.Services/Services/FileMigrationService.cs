using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Diagnostics;

namespace PlaySpace.Services.Services
{
    public class FileMigrationService : IFileMigrationService
    {
        private readonly IFtpStorageService _ftpStorageService;
        private readonly ILogger<FileMigrationService> _logger;
        private readonly string _uploadsBasePath;

        // Folders to migrate
        private readonly string[] _foldersToMigrate = new[]
        {
            "users",
            "avatars",
            "trainers",
            "facility-plans"
        };

        public FileMigrationService(IFtpStorageService ftpStorageService, ILogger<FileMigrationService> logger)
        {
            _ftpStorageService = ftpStorageService;
            _logger = logger;
            _uploadsBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        public async Task<FileMigrationResult> MigrateLocalFilesToFtpAsync(bool deleteLocalFiles = false)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new FileMigrationResult
            {
                LocalFilesDeleted = deleteLocalFiles
            };

            _logger.LogInformation("Starting file migration from local storage to FTP. Delete local files: {DeleteLocalFiles}", deleteLocalFiles);

            try
            {
                foreach (var folder in _foldersToMigrate)
                {
                    var localFolderPath = Path.Combine(_uploadsBasePath, folder);

                    if (!Directory.Exists(localFolderPath))
                    {
                        _logger.LogInformation("Local folder does not exist: {FolderPath}. Skipping.", localFolderPath);
                        continue;
                    }

                    _logger.LogInformation("Migrating files from folder: {FolderPath}", localFolderPath);

                    // Get all files in the folder
                    var files = Directory.GetFiles(localFolderPath, "*.*", SearchOption.TopDirectoryOnly);
                    result.TotalFiles += files.Length;

                    foreach (var filePath in files)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(filePath);
                            _logger.LogDebug("Migrating file: {FileName} from {Folder}", fileName, folder);

                            // Read file content
                            var fileBytes = await File.ReadAllBytesAsync(filePath);

                            // Upload to FTP
                            var uploadedUrl = await _ftpStorageService.UploadFileAsync(fileBytes, folder, fileName);

                            result.SuccessfulMigrations++;
                            result.MigratedFiles.Add($"{folder}/{fileName} -> {uploadedUrl}");
                            _logger.LogInformation("Successfully migrated: {FileName}", fileName);

                            // Delete local file if requested
                            if (deleteLocalFiles)
                            {
                                File.Delete(filePath);
                                _logger.LogDebug("Deleted local file: {FilePath}", filePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FailedMigrations++;
                            var error = $"Failed to migrate {Path.GetFileName(filePath)}: {ex.Message}";
                            result.Errors.Add(error);
                            _logger.LogError(ex, "Error migrating file: {FilePath}", filePath);
                        }
                    }
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                _logger.LogInformation(
                    "File migration completed. Total: {Total}, Successful: {Success}, Failed: {Failed}, Duration: {Duration}",
                    result.TotalFiles,
                    result.SuccessfulMigrations,
                    result.FailedMigrations,
                    result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during file migration");
                throw;
            }
        }

        public async Task<FileMigrationStatus> GetMigrationStatusAsync()
        {
            var status = new FileMigrationStatus();

            try
            {
                // Count local files
                foreach (var folder in _foldersToMigrate)
                {
                    var localFolderPath = Path.Combine(_uploadsBasePath, folder);

                    if (Directory.Exists(localFolderPath))
                    {
                        var fileCount = Directory.GetFiles(localFolderPath, "*.*", SearchOption.TopDirectoryOnly).Length;
                        status.LocalFilesCount += fileCount;
                        status.FilesByFolder[folder] = fileCount;
                        status.LocalFolders.Add(folder);

                        _logger.LogInformation("Local folder {Folder}: {Count} files", folder, fileCount);
                    }
                    else
                    {
                        status.FilesByFolder[folder] = 0;
                    }
                }

                // Count FTP files
                foreach (var folder in _foldersToMigrate)
                {
                    try
                    {
                        var ftpFiles = await _ftpStorageService.ListFilesAsync(folder);
                        status.FtpFilesCount += ftpFiles.Count;
                        _logger.LogInformation("FTP folder {Folder}: {Count} files", folder, ftpFiles.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not list FTP files for folder: {Folder}", folder);
                    }
                }

                _logger.LogInformation("Migration status: {LocalCount} local files, {FtpCount} FTP files",
                    status.LocalFilesCount, status.FtpFilesCount);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                throw;
            }
        }
    }
}
