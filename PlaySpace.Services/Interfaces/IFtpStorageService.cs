namespace PlaySpace.Services.Interfaces
{
    public interface IFtpStorageService
    {
        /// <summary>
        /// Upload a file from a stream to the FTP server
        /// </summary>
        /// <param name="fileStream">The file stream to upload</param>
        /// <param name="remotePath">The remote path (e.g., "uploads/users")</param>
        /// <param name="fileName">The filename to save as</param>
        /// <returns>The public URL to access the uploaded file</returns>
        Task<string> UploadFileAsync(Stream fileStream, string remotePath, string fileName);

        /// <summary>
        /// Upload a file from a byte array to the FTP server
        /// </summary>
        /// <param name="fileData">The file data as byte array</param>
        /// <param name="remotePath">The remote path (e.g., "uploads/avatars")</param>
        /// <param name="fileName">The filename to save as</param>
        /// <returns>The public URL to access the uploaded file</returns>
        Task<string> UploadFileAsync(byte[] fileData, string remotePath, string fileName);

        /// <summary>
        /// Delete a file from the FTP server
        /// </summary>
        /// <param name="remotePath">The full remote path including filename (e.g., "uploads/users/file.jpg")</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteFileAsync(string remotePath);

        /// <summary>
        /// Check if a file exists on the FTP server
        /// </summary>
        /// <param name="remotePath">The full remote path including filename</param>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> FileExistsAsync(string remotePath);

        /// <summary>
        /// List all files in a directory on the FTP server
        /// </summary>
        /// <param name="remotePath">The remote directory path</param>
        /// <returns>List of file names in the directory</returns>
        Task<List<string>> ListFilesAsync(string remotePath);

        /// <summary>
        /// Create a directory on the FTP server if it doesn't exist
        /// </summary>
        /// <param name="remotePath">The directory path to create</param>
        /// <returns>True if created or already exists, false otherwise</returns>
        Task<bool> CreateDirectoryAsync(string remotePath);
    }
}
