namespace WinService.Deploy.Models;

/// <summary>
/// Result of a deployment operation
/// </summary>
public class DeploymentResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupPath { get; set; }
    public int FilesCopied { get; set; }
    public TimeSpan Duration { get; set; }
    public bool ServiceRunning { get; set; }
}
