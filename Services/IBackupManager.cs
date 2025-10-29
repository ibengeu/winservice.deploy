namespace WinService.Deploy.Services;

/// <summary>
/// Interface for backup management operations
/// </summary>
public interface IBackupManager
{
    Task<string?> CreateBackupAsync(string sourcePath, string? versionTag = null, CancellationToken cancellationToken = default);
    Task<bool> RestoreBackupAsync(string backupPath, string destinationPath, CancellationToken cancellationToken = default);
    Task CleanOldBackupsAsync(string backupParentPath, int retentionDays, CancellationToken cancellationToken = default);
}
