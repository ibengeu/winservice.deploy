using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinService.Deploy.Models;
using System.Diagnostics;
using static System.ServiceProcess.ServiceControllerStatus;

namespace WinService.Deploy.Services;

/// <summary>
/// Main Windows Service deployment orchestration
/// </summary>
public class WindowsServiceDeployment : IDeploymentService
{
    private readonly ILogger<WindowsServiceDeployment> _logger;
    private readonly IServiceController _serviceController;
    private readonly IFileOperations _fileOperations;
    private readonly IBackupManager _backupManager;
    private readonly DeploymentSettings _settings;

    public WindowsServiceDeployment(
        ILogger<WindowsServiceDeployment> logger,
        IServiceController serviceController,
        IFileOperations fileOperations,
        IBackupManager backupManager,
        IOptions<DeploymentSettings> settings)
    {
        _logger = logger;
        _serviceController = serviceController;
        _fileOperations = fileOperations;
        _backupManager = backupManager;
        _settings = settings.Value;
    }

    public DeploymentSettings GetCurrentSettings() => _settings;

    public async Task<DeploymentResult> DeployAsync(
        DeploymentOptions options,
        IProgress<DeploymentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        string? backupPath = null;
        var result = new DeploymentResult();

        try
        {
            _logger.LogInformation("Starting deployment for service: {ServiceName}", options.ServiceName);
            LogToFile($"========== Deployment Started: {DateTime.Now} ==========");
            LogToFile($"Service: {options.ServiceName}");
            LogToFile($"Source: {options.SourcePath}");
            LogToFile($"Destination: {options.DestinationPath}");
            LogToFile($"WhatIf: {options.WhatIf}");

            // Validate
            if (!Directory.Exists(options.SourcePath))
            {
                throw new DirectoryNotFoundException($"Source path does not exist: {options.SourcePath}");
            }

            var computerName = ExtractComputerNameFromUncPath(options.DestinationPath);

            // Step 1: Stop Service
            ReportProgress(progress, "Stopping Windows Service...", 10, DeploymentStage.StoppingService);
            var stopServiceWatch = Stopwatch.StartNew();

            if (!options.WhatIf)
            {
                var stopped = await RetryOperationAsync(
                    () => _serviceController.StopServiceAsync(options.ServiceName, computerName, options.ServiceStopTimeoutSeconds, cancellationToken),
                    options.MaxRetries,
                    options.RetryDelaySeconds,
                    "Stop Service");

                stopServiceWatch.Stop();
                if (!stopped)
                {
                    throw new InvalidOperationException($"Failed to stop service: {options.ServiceName}");
                }
                LogToFile($"✓ Service stopped successfully (took {stopServiceWatch.ElapsedMilliseconds}ms)");
            }
            else
            {
                _logger.LogInformation("[WhatIf] Would stop service: {ServiceName}", options.ServiceName);
                LogToFile($"✓ Service stopped successfully");
            }

            // Step 2: Backup
            if (options.BackupEnabled)
            {
                ReportProgress(progress, "Creating backup...", 25, DeploymentStage.CreatingBackup);
                var backupWatch = Stopwatch.StartNew();

                if (!options.WhatIf)
                {
                    backupPath = await _backupManager.CreateBackupAsync(
                        options.DestinationPath,
                        options.VersionTag,
                        cancellationToken);

                    backupWatch.Stop();
                    result.BackupPath = backupPath;

                    if (backupPath != null)
                    {
                        LogToFile($"✓ Backup created: {backupPath} (took {backupWatch.ElapsedMilliseconds}ms)");
                    }
                    else
                    {
                        LogToFile("⚠ Backup skipped (destination doesn't exist yet)");
                    }
                }
                else
                {
                    _logger.LogInformation("[WhatIf] Would create backup at: {Path}", options.DestinationPath);
                }
            }

            // Step 3: Copy Files
            ReportProgress(progress, "Copying service files...", 40, DeploymentStage.CopyingFiles);
            var copyWatch = Stopwatch.StartNew();

            if (!options.WhatIf)
            {
                var copyProgress = new Progress<(int Current, int Total)>(p =>
                {
                    var percent = 40 + (int)((p.Current / (double)p.Total) * 30);
                    ReportProgress(progress, $"Copying files ({p.Current}/{p.Total})...", percent,
                        DeploymentStage.CopyingFiles);
                });

                var copyResult = await _fileOperations.CopyDirectoryAsync(
                    options.SourcePath,
                    options.DestinationPath,
                    copyProgress,
                    options.MaxDegreeOfParallelism,
                    cancellationToken);

                copyWatch.Stop();
                if (!copyResult.Success)
                {
                    throw new IOException("Failed to copy files");
                }

                result.FilesCopied = copyResult.FilesCopied;
                LogToFile($"✓ Copied {copyResult.FilesCopied} files successfully (took {copyWatch.ElapsedMilliseconds}ms)");
            }
            else
            {
                _logger.LogInformation("[WhatIf] Would copy files from {Source} to {Dest}",
                    options.SourcePath, options.DestinationPath);
            }

            // Step 4: Verify Files
            if (options.VerifyFilesAfterCopy && !options.WhatIf)
            {
                ReportProgress(progress, "Verifying file integrity...", 75, DeploymentStage.VerifyingFiles);
                var verifyWatch = Stopwatch.StartNew();

                var verified = await _fileOperations.VerifyFilesAsync(
                    options.SourcePath,
                    options.DestinationPath,
                    options.MaxDegreeOfParallelism,
                    cancellationToken);

                verifyWatch.Stop();
                if (!verified)
                {
                    throw new InvalidOperationException("File verification failed");
                }

                LogToFile($"✓ File integrity verified (took {verifyWatch.ElapsedMilliseconds}ms)");
            }

            // Step 5: Start Service
            ReportProgress(progress, "Starting Windows Service...", 85, DeploymentStage.StartingService);
            var startServiceWatch = Stopwatch.StartNew();

            if (!options.WhatIf)
            {
                var started = await RetryOperationAsync(
                    () => _serviceController.StartServiceAsync(options.ServiceName, computerName, options.ServiceStopTimeoutSeconds, cancellationToken),
                    options.MaxRetries,
                    options.RetryDelaySeconds,
                    "Start Service");

                if (!started)
                {
                    throw new InvalidOperationException($"Failed to start service: {options.ServiceName}");
                }

                var verifyDelayStart = startServiceWatch.ElapsedMilliseconds;
                await Task.Delay(5000, cancellationToken);
                var status = await _serviceController.GetServiceStatusAsync(options.ServiceName, computerName);
                result.ServiceRunning = status == Running;
                startServiceWatch.Stop();

                var verifyTime = startServiceWatch.ElapsedMilliseconds - verifyDelayStart;
                LogToFile($"✓ Service started successfully (Status: {status}, took {startServiceWatch.ElapsedMilliseconds}ms, verify: {verifyTime}ms)");
            }
            else
            {
                _logger.LogInformation("[WhatIf] Would start service: {ServiceName}", options.ServiceName);
            }

            // Step 6: Cleanup old backups
            if (options.BackupEnabled && backupPath != null && !options.WhatIf)
            {
                var cleanupWatch = Stopwatch.StartNew();
                var backupParent = Directory.GetParent(backupPath)?.FullName;
                if (backupParent != null)
                {
                    await _backupManager.CleanOldBackupsAsync(backupParent, options.BackupRetentionDays,
                        cancellationToken);
                    cleanupWatch.Stop();
                    LogToFile($"✓ Cleaned old backups (retention: {options.BackupRetentionDays} days, took {cleanupWatch.ElapsedMilliseconds}ms)");
                }
            }

            stopwatch.Stop();
            result.Success = true;
            result.Duration = stopwatch.Elapsed;

            ReportProgress(progress, "Deployment completed successfully!", 100, DeploymentStage.Completed);
            LogToFile($"========== Deployment Completed Successfully ==========");
            LogToFile($"Total Time: {stopwatch.Elapsed.TotalSeconds:F2}s ({stopwatch.ElapsedMilliseconds}ms)");
            LogToFile($"Performance Breakdown:");
            LogToFile($"  - Stop Service: {(stopServiceWatch?.ElapsedMilliseconds ?? 0)}ms");
            LogToFile($"  - Backup: {(options.BackupEnabled ? "included" : "skipped")}");
            LogToFile($"  - Copy Files: {(copyWatch?.ElapsedMilliseconds ?? 0)}ms");
            LogToFile($"  - Verify: {(options.VerifyFilesAfterCopy ? "included" : "skipped")}");
            LogToFile($"  - Start Service: {(startServiceWatch?.ElapsedMilliseconds ?? 0)}ms");
            LogToFile($"=======================================================");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Deployment failed");
            LogToFile($"✗ ERROR: {ex.Message}");

            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            // Attempt rollback if enabled
            if (options.EnableRollback && backupPath != null && !options.WhatIf)
            {
                _logger.LogWarning("Attempting rollback from backup...");
                LogToFile("⚠ Attempting rollback...");

                try
                {
                    var restored =
                        await _backupManager.RestoreBackupAsync(backupPath, options.DestinationPath, cancellationToken);

                    if (restored)
                    {
                        LogToFile("✓ Rollback completed, attempting to restart service...");

                        var computerName = ExtractComputerNameFromUncPath(options.DestinationPath);
                        var started = await RetryOperationAsync(
                            () => _serviceController.StartServiceAsync(options.ServiceName, computerName,
                                options.ServiceStopTimeoutSeconds, cancellationToken),
                            options.MaxRetries,
                            options.RetryDelaySeconds,
                            "Start Service After Rollback");

                        if (started)
                        {
                            LogToFile("✓ Service restarted after rollback");
                        }
                        else
                        {
                            LogToFile("✗ CRITICAL: Service failed to start after rollback");
                        }
                    }
                    else
                    {
                        LogToFile("✗ Rollback failed");
                    }
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback failed");
                    LogToFile($"✗ Rollback error: {rollbackEx.Message}");
                }
            }

            ReportProgress(progress, $"Deployment failed: {ex.Message}", 100, DeploymentStage.Failed);
            LogToFile($"========== Deployment Failed ==========");

            return result;
        }
    }

    private async Task<bool> RetryOperationAsync(
        Func<Task<bool>> operation,
        int maxRetries,
        int delaySeconds,
        string operationName)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting {Operation} (Attempt {Attempt}/{Max})",
                    operationName, attempt, maxRetries);

                var result = await operation();

                if (result)
                {
                    return true;
                }

                if (attempt < maxRetries)
                {
                    _logger.LogWarning("{Operation} failed, retrying in {Delay}s...",
                        operationName, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Operation} attempt {Attempt} failed", operationName, attempt);

                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }

        return false;
    }

    private void ReportProgress(
        IProgress<DeploymentProgress>? progress,
        string message,
        double percentComplete,
        DeploymentStage stage)
    {
        _logger.LogInformation("{Message} ({Percent}%)", message, percentComplete);
        progress?.Report(new DeploymentProgress
        {
            Message = message,
            PercentComplete = percentComplete,
            Stage = stage
        });
    }

    private static string? ExtractComputerNameFromUncPath(string path)
    {
        if (path.StartsWith(@"\\"))
        {
            var parts = path.TrimStart('\\').Split('\\');
            return parts.Length > 0 ? parts[0] : null;
        }

        return null;
    }

    private void LogToFile(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            File.AppendAllText(_settings.LogFilePath, logMessage + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to log file");
        }
    }
}