using WinService.Deploy.Models;

namespace WinService.Deploy.Services;

/// <summary>
/// Main deployment service interface
/// </summary>
public interface IDeploymentService
{
    Task<DeploymentResult> DeployAsync(DeploymentOptions options, IProgress<DeploymentProgress>? progress = null, CancellationToken cancellationToken = default);
    DeploymentSettings GetCurrentSettings();
}
