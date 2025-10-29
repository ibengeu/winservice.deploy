namespace WinService.Deploy.Services;

/// <summary>
/// Interface for file system operations
/// </summary>
public interface IFileOperations
{
    Task<(bool Success, int FilesCopied)> CopyDirectoryAsync(
        string sourcePath,
        string destinationPath,
        IProgress<(int Current, int Total)>? progress = null,
        int maxDegreeOfParallelism = 8,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyFilesAsync(string sourcePath, string destinationPath, int maxDegreeOfParallelism = 8, CancellationToken cancellationToken = default);
    Task<bool> DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default);
}
