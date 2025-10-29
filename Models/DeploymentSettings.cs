namespace WinService.Deploy.Models;

/// <summary>
/// Configuration settings loaded from appsettings.json
/// </summary>
public class DeploymentSettings
{
    public string ServiceName { get; set; } = "RevPayWorkerService";
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string? RemoteServer { get; set; }
    public bool BackupEnabled { get; set; } = true;
    public int BackupRetentionDays { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 10;
    public int ServiceStopTimeoutSeconds { get; set; } = 60;
    public int MaxDegreeOfParallelism { get; set; } = 8;
    public bool VerifyFilesAfterCopy { get; set; } = true;
    public bool EnableRollback { get; set; } = true;
    public string LogFilePath { get; set; } = "deployment.log";
}
