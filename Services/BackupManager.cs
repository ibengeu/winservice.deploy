using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinService.Deploy.Models;

namespace WinService.Deploy.Services;

/// <summary>
/// Backup management implementation
/// </summary>
public class BackupManager : IBackupManager
{
    private readonly ILogger<BackupManager> _logger;
    private readonly IFileOperations _fileOperations;
    private readonly DeploymentSettings _settings;

    public BackupManager(ILogger<BackupManager> logger, IFileOperations fileOperations, IOptions<DeploymentSettings> settings)
    {
        _logger = logger;
        _fileOperations = fileOperations;
        _settings = settings.Value;
    }

    public async Task<string?> CreateBackupAsync(string sourcePath, string? versionTag = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                _logger.LogWarning("Source path does not exist, skipping backup: {SourcePath}", sourcePath);
                return null;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFolderName = string.IsNullOrEmpty(versionTag)
                ? $"Backup_{timestamp}"
                : $"Backup_v{versionTag}_{timestamp}";

            var parentPath = Directory.GetParent(sourcePath)?.FullName;
            if (string.IsNullOrEmpty(parentPath))
            {
                _logger.LogError("Cannot determine parent directory for backup");
                return null;
            }

            var backupPath = Path.Combine(parentPath, "Backups", backupFolderName);

            _logger.LogInformation("Creating backup at: {BackupPath}", backupPath);

            var result = await _fileOperations.CopyDirectoryAsync(sourcePath, backupPath, null, _settings.MaxDegreeOfParallelism, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Backup created successfully with {Count} files", result.FilesCopied);
                return backupPath;
            }

            _logger.LogError("Backup creation failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup");
            return null;
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupPath, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Restoring from backup: {BackupPath}", backupPath);

            // Remove failed deployment
            if (Directory.Exists(destinationPath))
            {
                await _fileOperations.DeleteDirectoryAsync(destinationPath, cancellationToken);
            }

            // Restore from backup
            var result = await _fileOperations.CopyDirectoryAsync(backupPath, destinationPath, null, _settings.MaxDegreeOfParallelism, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Rollback completed successfully");
                return true;
            }

            _logger.LogError("Rollback failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup");
            return false;
        }
    }

    public async Task CleanOldBackupsAsync(string backupParentPath, int retentionDays,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(backupParentPath))
            {
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var backupFolders = Directory.GetDirectories(backupParentPath, "Backup_*");

            foreach (var folder in backupFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var folderInfo = new DirectoryInfo(folder);
                if (folderInfo.CreationTime < cutoffDate)
                {
                    _logger.LogInformation("Removing old backup: {FolderName}", folderInfo.Name);
                    await _fileOperations.DeleteDirectoryAsync(folder, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean old backups");
        }
    }
}