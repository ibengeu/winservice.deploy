using System.ServiceProcess;

namespace WinService.Deploy.Services;

/// <summary>
/// Interface for Windows Service control operations
/// </summary>
public interface IServiceController
{
    Task<bool> StopServiceAsync(string serviceName, string? computerName = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default);
    Task<bool> StartServiceAsync(string serviceName, string? computerName = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default);
    Task<ServiceControllerStatus?> GetServiceStatusAsync(string serviceName, string? computerName = null);
}
