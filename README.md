# WinService.Deploy

A general-purpose .NET 8 console application for automated Windows Service deployment with backup, rollback, and comprehensive logging.

## Features

- ✅ **Interactive Console UI** - Beautiful menu-driven interface with Spectre.Console
- ✅ **Automatic Backup** - Creates versioned backups before deployment
- ✅ **File Integrity Verification** - BLAKE3 hash verification after copy
- ✅ **Automatic Rollback** - Restores from backup if deployment fails
- ✅ **Retry Logic** - Configurable retry attempts for service operations
- ✅ **Progress Tracking** - Real-time progress indicators
- ✅ **Comprehensive Logging** - Timestamped logs with deployment history
- ✅ **WhatIf Mode** - Test deployments without making changes
- ✅ **Backup Cleanup** - Automatically removes old backups

## Quick Start

### 1. Build the Tool

```bash
cd WinService.Deploy  # or wherever you placed the tool
dotnet build -c Release
```

### 2. Configure Settings

Edit `appsettings.json`:

```json
{
  "DeploymentSettings": {
    "ServiceName": "YourWindowsServiceName",
    "SourcePath": "C:\\Path\\To\\Your\\Service\\bin\\Release\\net8.0",
    "DestinationPath": "\\\\YOUR_SERVER\\c$\\Services\\YourService",
    "BackupEnabled": true,
    "BackupRetentionDays": 30,
    "MaxRetries": 3,
    "VerifyFilesAfterCopy": true
  }
}
```

### 3. Run the Tool

```bash
cd bin\Release\net8.0
.\WinService.Deploy.exe
```

## Usage Modes

### Quick Deploy
- Minimal prompts
- Uses sensible defaults
- Fastest way to deploy

### Custom Deploy
- Full control over all options
- Configure retries, backups, verification
- Advanced deployment scenarios

### Test Deploy (WhatIf)
- Simulates deployment without making changes
- Safe way to test before actual deployment
- Shows what would happen

## Deployment Workflow

1. **Stop Service** - Gracefully stops the Windows Service with retry logic
2. **Create Backup** - Backs up existing files with version tag
3. **Copy Files** - Copies new service files to destination
4. **Verify Integrity** - Verifies file hashes (optional)
5. **Start Service** - Restarts the service and verifies it's running
6. **Cleanup** - Removes old backups based on retention policy

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `ServiceName` | Windows Service name | YourWindowsServiceName |
| `SourcePath` | Build output directory | C:\Path\To\Your\Service\bin\Release\net8.0 |
| `DestinationPath` | Target deployment directory | Must be set |
| `BackupEnabled` | Enable automatic backup | true |
| `BackupRetentionDays` | Days to keep old backups | 30 |
| `MaxRetries` | Retry attempts for service ops | 3 |
| `RetryDelaySeconds` | Delay between retries | 10 |
| `VerifyFilesAfterCopy` | Verify file integrity | true |
| `EnableRollback` | Auto-rollback on failure | true |

## Logging

Logs are written to `deployment.log` in the application directory with the following format:

```
[2025-10-29 14:30:15] ========== Deployment Started: 10/29/2025 2:30:15 PM ==========
[2025-10-29 14:30:15] Service: YourWindowsServiceName
[2025-10-29 14:30:15] Source: C:\Build\Output
[2025-10-29 14:30:15] Destination: \\server\c$\Services\YourService
[2025-10-29 14:30:17] ✓ Service stopped successfully
[2025-10-29 14:30:20] ✓ Backup created: \\server\c$\Services\Backup_v1.0.5_20251029_143020
[2025-10-29 14:30:45] ✓ Copied 156 files successfully
[2025-10-29 14:30:52] ✓ File integrity verified
[2025-10-29 14:30:58] ✓ Service started successfully (Status: Running)
[2025-10-29 14:31:00] ========== Deployment Completed Successfully in 45.23s ==========
```

## Error Handling

The tool handles errors gracefully:

- **Service Won't Stop**: Retries up to MaxRetries times
- **Copy Failure**: Rolls back to backup (if enabled)
- **Service Won't Start**: Attempts rollback and restart
- **Verification Failure**: Rolls back deployment

## Examples

### Deploy to Local Server
```bash
# Run the tool
.\WinService.Deploy.exe

# Select: Quick Deploy
# Service Name: YourWindowsServiceName
# Source Path: C:\Path\To\Your\Service\bin\Release\net8.0
# Destination: C:\Services\YourService
```

### Deploy to Remote Server (UNC)
```bash
# Run the tool
.\WinService.Deploy.exe

# Select: Quick Deploy
# Destination: \\prodserver\c$\Services\YourService
```

### Test Deployment
```bash
# Run the tool
.\WinService.Deploy.exe

# Select: Test Deploy (WhatIf)
# Review what would happen without making changes
```

## Requirements

- .NET 8.0 Runtime
- Windows OS
- Administrator privileges (for service control)
- Network access to target server (if remote)

## Troubleshooting

### "Access Denied"
- Run as Administrator
- Check network share permissions
- Ensure proper credentials

### "Service Not Found"
- Verify service name is correct
- Check service exists on target server

### "Backup Failed"
- Check destination path exists
- Verify sufficient disk space
- Ensure write permissions

## Architecture

- **Models**: Data transfer objects for configuration and results
- **Services**:
  - `WindowsServiceDeployment`: Main orchestration
  - `WindowsServiceController`: Service control operations
  - `FileOperations`: File copy and verification
  - `BackupManager`: Backup/restore operations
- **Dependency Injection**: Full DI container with logging
- **Configuration**: appsettings.json with override support

## Development

```bash
# Build
dotnet build

# Run in debug mode
dotnet run

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
```

## Use Across Projects

This tool is designed to be project-agnostic and can be used for any Windows Service deployment:

1. Copy the entire tool directory to your solution or keep it in a shared location
2. Update `appsettings.json` with your project-specific paths and service name
3. Run the tool from any location

You can maintain separate `appsettings.json` configurations for different projects or use the interactive prompts to specify paths at runtime.
