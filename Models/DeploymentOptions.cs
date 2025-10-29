namespace WinService.Deploy.Models;

/// <summary>
/// Runtime deployment options that can override settings
/// </summary>
public class DeploymentOptions
{
    public string ServiceName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string? RemoteServer { get; set; }
    public bool BackupEnabled { get; set; } = true;
    public string? VersionTag { get; set; }
    public int BackupRetentionDays { get; set; } = 30;
    public bool VerifyFilesAfterCopy { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 10;
    public int ServiceStopTimeoutSeconds { get; set; } = 60;
    public int MaxDegreeOfParallelism { get; set; } = 8;
    public bool EnableRollback { get; set; } = true;
    public bool WhatIf { get; set; } = false;
}
