using Microsoft.Extensions.Logging;
using WinService.Deploy.Models;
using System.ServiceProcess;

namespace WinService.Deploy.Services;

/// <summary>
/// Windows Service control implementation with retry logic
/// </summary>
#pragma warning disable CA1416 // Validate platform compatibility
public class WindowsServiceController : IServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;

    public WindowsServiceController(ILogger<WindowsServiceController> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StopServiceAsync(string serviceName, string? computerName = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Stopping service: {ServiceName} (timeout: {Timeout}s)", serviceName, timeoutSeconds);

            using var service = string.IsNullOrEmpty(computerName)
                ? new ServiceController(serviceName)
                : new ServiceController(serviceName, computerName);

            // Check initial status
            var initialStatusTime = stopwatch.ElapsedMilliseconds;
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("Service {ServiceName} is already stopped (checked in {Ms}ms)",
                    serviceName, initialStatusTime);
                return true;
            }

            _logger.LogInformation("Service {ServiceName} current status: {Status} (checked in {Ms}ms)",
                serviceName, service.Status, initialStatusTime);

            // Send stop command
            if (service.Status != ServiceControllerStatus.StopPending)
            {
                var beforeStop = stopwatch.ElapsedMilliseconds;
                service.Stop();
                var afterStop = stopwatch.ElapsedMilliseconds;
                _logger.LogInformation("Stop command sent to {ServiceName} (took {Ms}ms)",
                    serviceName, afterStop - beforeStop);
            }

            // Wait for service to stop with timeout
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var waitStart = stopwatch.ElapsedMilliseconds;
            await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Stopped, timeout), cancellationToken);
            var waitEnd = stopwatch.ElapsedMilliseconds;

            stopwatch.Stop();
            _logger.LogInformation("Service {ServiceName} stopped successfully (wait: {WaitMs}ms, total: {TotalMs}ms)",
                serviceName, waitEnd - waitStart, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to stop service: {ServiceName} (after {Ms}ms)",
                serviceName, stopwatch.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<bool> StartServiceAsync(string serviceName, string? computerName = null, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Starting service: {ServiceName} (timeout: {Timeout}s)", serviceName, timeoutSeconds);

            using var service = string.IsNullOrEmpty(computerName)
                ? new ServiceController(serviceName)
                : new ServiceController(serviceName, computerName);

            // Check initial status
            var initialStatusTime = stopwatch.ElapsedMilliseconds;
            if (service.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("Service {ServiceName} is already running (checked in {Ms}ms)",
                    serviceName, initialStatusTime);
                return true;
            }

            _logger.LogInformation("Service {ServiceName} current status: {Status} (checked in {Ms}ms)",
                serviceName, service.Status, initialStatusTime);

            // Send start command
            if (service.Status != ServiceControllerStatus.StartPending)
            {
                var beforeStart = stopwatch.ElapsedMilliseconds;
                service.Start();
                var afterStart = stopwatch.ElapsedMilliseconds;
                _logger.LogInformation("Start command sent to {ServiceName} (took {Ms}ms)",
                    serviceName, afterStart - beforeStart);
            }

            // Wait for service to start with timeout
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var waitStart = stopwatch.ElapsedMilliseconds;
            await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Running, timeout), cancellationToken);
            var waitEnd = stopwatch.ElapsedMilliseconds;

            stopwatch.Stop();
            _logger.LogInformation("Service {ServiceName} started successfully (wait: {WaitMs}ms, total: {TotalMs}ms)",
                serviceName, waitEnd - waitStart, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to start service: {ServiceName} (after {Ms}ms)",
                serviceName, stopwatch.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<ServiceControllerStatus?> GetServiceStatusAsync(string serviceName, string? computerName = null)
    {
        try
        {
            using var service = string.IsNullOrEmpty(computerName)
                ? new ServiceController(serviceName)
                : new ServiceController(serviceName, computerName);

            await Task.Run(() => service.Refresh());
            return service.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for service: {ServiceName}", serviceName);
            return null;
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
