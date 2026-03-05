using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;
using Renci.SshNet;

namespace PlaySpace.Services.Services
{
    public class FtpStorageService : IFtpStorageService
    {
        private readonly FtpConfiguration _config;
        private readonly ILogger<FtpStorageService> _logger;

        public FtpStorageService(IOptions<FtpConfiguration> config, ILogger<FtpStorageService> logger)
        {
            _config = config.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_config.Host))
                throw new InvalidOperationException("SFTP Host is not configured");
            if (string.IsNullOrEmpty(_config.Username))
                throw new InvalidOperationException("SFTP Username is not configured");
            if (string.IsNullOrEmpty(_config.Password))
                throw new InvalidOperationException("SFTP Password is not configured");
            if (string.IsNullOrEmpty(_config.PublicUrlBase))
                throw new InvalidOperationException("SFTP PublicUrlBase is not configured");

            _logger.LogInformation("SftpStorageService initialized with host: {Host}, user: {Username}",
                _config.Host, _config.Username);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string remotePath, string fileName)
        {
            return await Task.Run(() =>
            {
                using var client = CreateSftpClient();
                try
                {
                    client.Connect();
                    _logger.LogDebug("SFTP client connected to {Host}", _config.Host);

                    var fullRemotePath = CombinePath(_config.BaseDirectory, remotePath);
                    EnsureDirectoryExists(client, fullRemotePath);

                    var fullFilePath = CombinePath(fullRemotePath, fileName);
                    _logger.LogInformation("Uploading file via SFTP: {FilePath}", fullFilePath);

                    client.UploadFile(fileStream, fullFilePath, true);

                    _logger.LogInformation("File uploaded successfully via SFTP: {FilePath}", fullFilePath);
                    return BuildPublicUrl(remotePath, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file via SFTP: {RemotePath}/{FileName}", remotePath, fileName);
                    throw new InvalidOperationException($"Failed to upload file via SFTP: {ex.Message}", ex);
                }
                finally
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
            });
        }

        public async Task<string> UploadFileAsync(byte[] fileData, string remotePath, string fileName)
        {
            using var memoryStream = new MemoryStream(fileData);
            return await UploadFileAsync(memoryStream, remotePath, fileName);
        }

        public async Task<bool> DeleteFileAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                using var client = CreateSftpClient();
                try
                {
                    client.Connect();
                    var fullRemotePath = CombinePath(_config.BaseDirectory, remotePath);
                    _logger.LogInformation("Deleting file via SFTP: {FilePath}", fullRemotePath);

                    if (!client.Exists(fullRemotePath))
                    {
                        _logger.LogWarning("File does not exist on SFTP: {FilePath}", fullRemotePath);
                        return false;
                    }

                    client.DeleteFile(fullRemotePath);
                    _logger.LogInformation("File deleted successfully via SFTP: {FilePath}", fullRemotePath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting file via SFTP: {RemotePath}", remotePath);
                    return false;
                }
                finally
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
            });
        }

        public async Task<bool> FileExistsAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                using var client = CreateSftpClient();
                try
                {
                    client.Connect();
                    var fullRemotePath = CombinePath(_config.BaseDirectory, remotePath);
                    return client.Exists(fullRemotePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking if file exists via SFTP: {RemotePath}", remotePath);
                    return false;
                }
                finally
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
            });
        }

        public async Task<List<string>> ListFilesAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                using var client = CreateSftpClient();
                try
                {
                    client.Connect();
                    var fullRemotePath = CombinePath(_config.BaseDirectory, remotePath);
                    _logger.LogInformation("Listing files via SFTP: {DirectoryPath}", fullRemotePath);

                    var files = client.ListDirectory(fullRemotePath)
                        .Where(f => f.IsRegularFile)
                        .Select(f => f.Name)
                        .ToList();

                    _logger.LogInformation("Found {Count} files in SFTP directory: {DirectoryPath}", files.Count, fullRemotePath);
                    return files;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing files via SFTP: {RemotePath}", remotePath);
                    return new List<string>();
                }
                finally
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
            });
        }

        public async Task<bool> CreateDirectoryAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                using var client = CreateSftpClient();
                try
                {
                    client.Connect();
                    var fullRemotePath = CombinePath(_config.BaseDirectory, remotePath);
                    EnsureDirectoryExists(client, fullRemotePath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating directory via SFTP: {RemotePath}", remotePath);
                    return false;
                }
                finally
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
            });
        }

        private SftpClient CreateSftpClient()
        {
            return new SftpClient(_config.Host, _config.Port, _config.Username, _config.Password)
            {
                ConnectionInfo =
                {
                    Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
                }
            };
        }

        private void EnsureDirectoryExists(SftpClient client, string directoryPath)
        {
            // Only create subdirectories within BaseDirectory — do not touch parent system dirs
            var baseDir = _config.BaseDirectory.TrimEnd('/');
            var relativePart = directoryPath.StartsWith(baseDir)
                ? directoryPath.Substring(baseDir.Length)
                : directoryPath;

            var parts = relativePart.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = baseDir;

            foreach (var part in parts)
            {
                current += "/" + part;
                if (!client.Exists(current))
                {
                    _logger.LogInformation("Creating SFTP directory: {Directory}", current);
                    client.CreateDirectory(current);
                }
            }
        }

        private string CombinePath(string path1, string path2)
        {
            path1 = path1.TrimEnd('/');
            path2 = path2.TrimStart('/');
            return $"{path1}/{path2}";
        }

        private string BuildPublicUrl(string remotePath, string fileName)
        {
            var urlBase = _config.PublicUrlBase.TrimEnd('/');
            var path = remotePath.TrimStart('/').TrimEnd('/');
            return $"{urlBase}/uploads/{path}/{fileName}";
        }
    }
}
