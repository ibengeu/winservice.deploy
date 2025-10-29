using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinService.Deploy.Models;
using WinService.Deploy.Services;
using Spectre.Console;
using System.Reflection;

namespace WinService.Deploy;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Display banner
            DisplayBanner();

            // Build host
            var host = CreateHostBuilder(args).Build();

            // Get deployment service
            var deploymentService = host.Services.GetRequiredService<IDeploymentService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Run interactive menu
            var exitCode = await RunInteractiveMenu(deploymentService, logger);

            return exitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fatal error: {ex.Message}[/]");
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Register deployment settings
                services.Configure<DeploymentSettings>(
                    context.Configuration.GetSection("DeploymentSettings"));

                // Register services
                services.AddSingleton<IDeploymentService, WindowsServiceDeployment>();
                services.AddSingleton<IServiceController, WindowsServiceController>();
                services.AddSingleton<IFileOperations, FileOperations>();
                services.AddSingleton<IBackupManager, BackupManager>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            });

    static void DisplayBanner()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        AnsiConsole.Write(
            new FigletText("WinService Deploy")
                .LeftJustified()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine($"[grey]Version {version}[/]");
        AnsiConsole.MarkupLine($"[grey]Windows Service Deployment Tool[/]");
        AnsiConsole.WriteLine();
    }

    static async Task<int> RunInteractiveMenu(IDeploymentService deploymentService, ILogger<Program> logger)
    {
        while (true)
        {
            AnsiConsole.Clear();
            DisplayBanner();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "Quick Deploy",
                        "Custom Deploy",
                        "Test Deploy (WhatIf)",
                        "View Deployment Log",
                        "View Configuration",
                        "Exit"
                    }));

            try
            {
                switch (choice)
                {
                    case "Quick Deploy":
                        await HandleQuickDeploy(deploymentService);
                        break;

                    case "Custom Deploy":
                        await HandleCustomDeploy(deploymentService);
                        break;

                    case "Test Deploy (WhatIf)":
                        await HandleTestDeploy(deploymentService);
                        break;

                    case "View Deployment Log":
                        HandleViewLog();
                        break;

                    case "View Configuration":
                        HandleViewConfiguration(deploymentService);
                        break;

                    case "Exit":
                        AnsiConsole.MarkupLine("[yellow]Exiting...[/]");
                        return 0;
                }

                if (choice != "Exit")
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                    Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during {Operation}", choice);
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }
    }

    static async Task HandleQuickDeploy(IDeploymentService deploymentService)
    {
        AnsiConsole.MarkupLine("[cyan]Quick Deploy Mode[/]");
        AnsiConsole.WriteLine();

        // Get default values from configuration
        var currentSettings = deploymentService.GetCurrentSettings();

        // Get basic inputs
        var serviceName = AnsiConsole.Ask<string>("Service Name:", currentSettings.ServiceName ?? "RevPayWorkerService");
        var sourcePath = AnsiConsole.Ask<string>("Source Path:", currentSettings.SourcePath ?? string.Empty);
        var destinationPath = AnsiConsole.Ask<string>("Destination Path:", currentSettings.DestinationPath ?? string.Empty);

        if (!Directory.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source path does not exist: {sourcePath}[/]");
            return;
        }

        var enableBackup = AnsiConsole.Confirm("Enable backup?", true);
        var versionTag = enableBackup ? AnsiConsole.Ask<string>("Version tag (optional):", string.Empty) : string.Empty;

        // Show summary
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Service Name", serviceName);
        table.AddRow("Source Path", sourcePath);
        table.AddRow("Destination Path", destinationPath);
        table.AddRow("Backup Enabled", enableBackup.ToString());
        if (!string.IsNullOrEmpty(versionTag))
            table.AddRow("Version Tag", versionTag);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Proceed with deployment?", true))
        {
            AnsiConsole.MarkupLine("[yellow]Deployment cancelled[/]");
            return;
        }

        // Execute deployment
        var options = new DeploymentOptions
        {
            ServiceName = serviceName,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            BackupEnabled = enableBackup,
            VersionTag = string.IsNullOrEmpty(versionTag) ? null : versionTag,
            WhatIf = false
        };

        await ExecuteDeployment(deploymentService, options);
    }

    static async Task HandleCustomDeploy(IDeploymentService deploymentService)
    {
        AnsiConsole.MarkupLine("[cyan]Custom Deploy Mode[/]");
        AnsiConsole.WriteLine();

        // Get default values from configuration
        var currentSettings = deploymentService.GetCurrentSettings();

        var serviceName = AnsiConsole.Ask<string>("Service Name:", currentSettings.ServiceName ?? "RevPayWorkerService");
        var sourcePath = AnsiConsole.Ask<string>("Source Path:", currentSettings.SourcePath ?? string.Empty);
        var destinationPath = AnsiConsole.Ask<string>("Destination Path:", currentSettings.DestinationPath ?? string.Empty);

        if (!Directory.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source path does not exist: {sourcePath}[/]");
            return;
        }

        var remoteServer = AnsiConsole.Ask<string>("Remote Server (optional, leave empty for UNC):", string.Empty);
        var enableBackup = AnsiConsole.Confirm("Enable backup?", true);

        string? versionTag = null;
        int backupRetentionDays = 30;

        if (enableBackup)
        {
            versionTag = AnsiConsole.Ask<string>("Version tag (optional):", string.Empty);
            if (string.IsNullOrEmpty(versionTag)) versionTag = null;
            backupRetentionDays = AnsiConsole.Ask<int>("Backup retention days:", 30);
        }

        var verifyFiles = AnsiConsole.Confirm("Verify file integrity after copy?", true);
        var maxRetries = AnsiConsole.Ask<int>("Max retry attempts:", 3);
        var retryDelay = AnsiConsole.Ask<int>("Retry delay (seconds):", 10);
        var enableRollback = AnsiConsole.Confirm("Enable automatic rollback on failure?", true);

        // Show summary
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Service Name", serviceName);
        table.AddRow("Source Path", sourcePath);
        table.AddRow("Destination Path", destinationPath);
        if (!string.IsNullOrEmpty(remoteServer))
            table.AddRow("Remote Server", remoteServer);
        table.AddRow("Backup Enabled", enableBackup.ToString());
        if (versionTag != null)
            table.AddRow("Version Tag", versionTag);
        if (enableBackup)
            table.AddRow("Backup Retention", $"{backupRetentionDays} days");
        table.AddRow("Verify Files", verifyFiles.ToString());
        table.AddRow("Max Retries", maxRetries.ToString());
        table.AddRow("Retry Delay", $"{retryDelay}s");
        table.AddRow("Enable Rollback", enableRollback.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Proceed with deployment?", true))
        {
            AnsiConsole.MarkupLine("[yellow]Deployment cancelled[/]");
            return;
        }

        var options = new DeploymentOptions
        {
            ServiceName = serviceName,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            RemoteServer = string.IsNullOrEmpty(remoteServer) ? null : remoteServer,
            BackupEnabled = enableBackup,
            VersionTag = versionTag,
            BackupRetentionDays = backupRetentionDays,
            VerifyFilesAfterCopy = verifyFiles,
            MaxRetries = maxRetries,
            RetryDelaySeconds = retryDelay,
            EnableRollback = enableRollback,
            WhatIf = false
        };

        await ExecuteDeployment(deploymentService, options);
    }

    static async Task HandleTestDeploy(IDeploymentService deploymentService)
    {
        AnsiConsole.MarkupLine("[yellow]Test Deploy Mode (WhatIf)[/]");
        AnsiConsole.WriteLine();

        // Get default values from configuration
        var currentSettings = deploymentService.GetCurrentSettings();

        var serviceName = AnsiConsole.Ask<string>("Service Name:", currentSettings.ServiceName ?? "RevPayWorkerService");
        var sourcePath = AnsiConsole.Ask<string>("Source Path:", currentSettings.SourcePath ?? string.Empty);
        var destinationPath = AnsiConsole.Ask<string>("Destination Path:", currentSettings.DestinationPath ?? string.Empty);

        if (!Directory.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source path does not exist: {sourcePath}[/]");
            return;
        }

        var options = new DeploymentOptions
        {
            ServiceName = serviceName,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            WhatIf = true
        };

        AnsiConsole.MarkupLine("[yellow]*** TEST MODE - No changes will be made ***[/]");
        AnsiConsole.WriteLine();

        await ExecuteDeployment(deploymentService, options);
    }

    static async Task ExecuteDeployment(IDeploymentService deploymentService, DeploymentOptions options)
    {
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Deploying service...[/]");

                try
                {
                    var result = await deploymentService.DeployAsync(options,
                        new Progress<DeploymentProgress>(progress =>
                        {
                            task.Description = $"[green]{progress.Message}[/]";
                            task.Value = progress.PercentComplete;
                        }));

                    task.StopTask();

                    if (result.Success)
                    {
                        AnsiConsole.MarkupLine("[green]✓ Deployment completed successfully![/]");
                        AnsiConsole.MarkupLine($"[grey]Duration: {result.Duration.TotalSeconds:F2}s[/]");
                        AnsiConsole.MarkupLine($"[grey]Files copied: {result.FilesCopied}[/]");
                        if (result.BackupPath != null)
                            AnsiConsole.MarkupLine($"[grey]Backup: {result.BackupPath}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Deployment failed: {result.ErrorMessage}[/]");
                    }
                }
                catch (Exception ex)
                {
                    task.StopTask();
                    AnsiConsole.MarkupLine($"[red]✗ Deployment failed: {ex.Message}[/]");
                }
            });
    }

    static void HandleViewLog()
    {
        var logPath = "deployment.log";

        if (!File.Exists(logPath))
        {
            AnsiConsole.MarkupLine($"[yellow]Log file not found: {logPath}[/]");
            return;
        }

        AnsiConsole.MarkupLine("[cyan]Last 50 lines of deployment log:[/]");
        AnsiConsole.WriteLine();

        var lines = File.ReadLines(logPath).Reverse().Take(50).Reverse();

        foreach (var line in lines)
        {
            if (line.Contains("ERROR") || line.Contains("✗"))
                AnsiConsole.MarkupLine($"[red]{line.EscapeMarkup()}[/]");
            else if (line.Contains("WARNING") || line.Contains("⚠"))
                AnsiConsole.MarkupLine($"[yellow]{line.EscapeMarkup()}[/]");
            else if (line.Contains("✓"))
                AnsiConsole.MarkupLine($"[green]{line.EscapeMarkup()}[/]");
            else
                AnsiConsole.MarkupLine($"[grey]{line.EscapeMarkup()}[/]");
        }
    }

    static void HandleViewConfiguration(IDeploymentService deploymentService)
    {
        var settings = deploymentService.GetCurrentSettings();

        var table = new Table();
        table.Title = new TableTitle("[cyan]Current Configuration[/]");
        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("Service Name", settings.ServiceName ?? "Not set");
        table.AddRow("Source Path", settings.SourcePath ?? "Not set");
        table.AddRow("Destination Path", settings.DestinationPath ?? "Not set");
        table.AddRow("Remote Server", settings.RemoteServer ?? "Not set (UNC mode)");
        table.AddRow("Backup Enabled", settings.BackupEnabled.ToString());
        table.AddRow("Backup Retention", $"{settings.BackupRetentionDays} days");
        table.AddRow("Max Retries", settings.MaxRetries.ToString());
        table.AddRow("Retry Delay", $"{settings.RetryDelaySeconds}s");
        table.AddRow("Verify Files", settings.VerifyFilesAfterCopy.ToString());
        table.AddRow("Enable Rollback", settings.EnableRollback.ToString());
        table.AddRow("Log File Path", settings.LogFilePath ?? "deployment.log");

        AnsiConsole.Write(table);
    }
}