using Microsoft.Extensions.Logging;
using Blake3;

namespace WinService.Deploy.Services;

/// <summary>
/// File system operations implementation
/// </summary>
public class FileOperations : IFileOperations
{
    private readonly ILogger<FileOperations> _logger;

    public FileOperations(ILogger<FileOperations> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, int FilesCopied)> CopyDirectoryAsync(
        string sourcePath,
        string destinationPath,
        IProgress<(int Current, int Total)>? progress = null,
        int maxDegreeOfParallelism = 8,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Copying files from {Source} to {Destination} (parallel: {Parallelism})",
                sourcePath, destinationPath, maxDegreeOfParallelism);

            if (!Directory.Exists(sourcePath))
            {
                _logger.LogError("Source directory does not exist: {SourcePath}", sourcePath);
                return (false, 0);
            }

            // Ensure destination exists
            Directory.CreateDirectory(destinationPath);

            // Get all files to copy
            var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var totalFiles = sourceFiles.Length;
            var copiedFiles = 0;

            // Use parallel processing for file copy
            await Parallel.ForEachAsync(
                sourceFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (sourceFile, ct) =>
                {
                    var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
                    var destFile = Path.Combine(destinationPath, relativePath);
                    var destDir = Path.GetDirectoryName(destFile);

                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    await Task.Run(() => File.Copy(sourceFile, destFile, true), ct);

                    var currentCount = Interlocked.Increment(ref copiedFiles);
                    progress?.Report((currentCount, totalFiles));

                    if (currentCount % 10 == 0)
                    {
                        _logger.LogDebug("Copied {Copied} of {Total} files", currentCount, totalFiles);
                    }
                });

            _logger.LogInformation("Successfully copied {Count} files in parallel", copiedFiles);
            return (true, copiedFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy directory from {Source} to {Destination}", sourcePath, destinationPath);
            return (false, 0);
        }
    }

    public async Task<bool> VerifyFilesAsync(string sourcePath, string destinationPath, int maxDegreeOfParallelism = 8, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying file integrity using BLAKE3 (parallel: {Parallelism})", maxDegreeOfParallelism);

            var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var verificationFailed = false;
            var failureReason = string.Empty;

            // Use parallel processing for file verification
            await Parallel.ForEachAsync(
                sourceFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (sourceFile, ct) =>
                {
                    // Early exit if verification already failed
                    if (verificationFailed) return;

                    var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
                    var destFile = Path.Combine(destinationPath, relativePath);

                    if (!File.Exists(destFile))
                    {
                        _logger.LogError("Missing file in destination: {RelativePath}", relativePath);
                        verificationFailed = true;
                        failureReason = $"Missing file: {relativePath}";
                        return;
                    }

                    var sourceHash = await ComputeFileHashBlake3Async(sourceFile, ct);
                    var destHash = await ComputeFileHashBlake3Async(destFile, ct);

                    if (sourceHash != destHash)
                    {
                        _logger.LogError("Hash mismatch for file: {RelativePath}", relativePath);
                        verificationFailed = true;
                        failureReason = $"Hash mismatch: {relativePath}";
                    }
                });

            if (verificationFailed)
            {
                _logger.LogError("File verification failed: {Reason}", failureReason);
                return false;
            }

            _logger.LogInformation("File verification completed successfully ({Count} files)", sourceFiles.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File verification failed");
            return false;
        }
    }

    public async Task<bool> DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            _logger.LogInformation("Deleting directory: {Path}", path);
            await Task.Run(() => Directory.Delete(path, true), cancellationToken);
            _logger.LogInformation("Directory deleted successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete directory: {Path}", path);
            return false;
        }
    }

    private async Task<string> ComputeFileHashBlake3Async(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var hasher = Hasher.New();

        byte[] buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, bytesRead));
        }

        var hash = hasher.Finalize();
        return Convert.ToHexString(hash.AsSpan());
    }
}
