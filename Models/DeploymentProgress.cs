namespace WinService.Deploy.Models;

/// <summary>
/// Progress information for deployment operations
/// </summary>
public class DeploymentProgress
{
    public string Message { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public DeploymentStage Stage { get; set; }
}

public enum DeploymentStage
{
    Starting,
    StoppingService,
    CreatingBackup,
    CopyingFiles,
    VerifyingFiles,
    StartingService,
    Completed,
    Failed
}
