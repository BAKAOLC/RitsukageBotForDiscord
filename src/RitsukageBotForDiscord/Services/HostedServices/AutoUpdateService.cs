using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
using RitsukageBot.Library.Utils;
using RitsukageBot.Options;
using RitsukageBot.Services.Providers;
using FileMode = System.IO.FileMode;

namespace RitsukageBot.Services.HostedServices
{
    /// <summary>
    ///     Auto update service.
    ///     This service will check for new version of the bot and update itself.
    ///     Note: This service will only check for updates when logged in to GitHub.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="gitHubClientProviderService"></param>
    /// <param name="configuration"></param>
    public partial class AutoUpdateService(
        ILogger<AutoUpdateService> logger,
        GitHubClientProviderService gitHubClientProviderService,
        IConfiguration configuration)
        : IHostedService
    {
        private readonly AutoUpdateOption _option =
            configuration.GetSection("AutoUpdate").Get<AutoUpdateOption>() ?? new();

        private Timer? _timer;

        private string RepositoryOwner => _option.Information.RepositoryOwner;

        private string RepositoryName => _option.Information.RepositoryName;

        private string BranchName => _option.Information.BranchName;

        private string TargetJobName => string.Format(_option.Information.TargetJobName, TargetOsArtifactPlatform);

        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        private static string TargetOsArtifactPlatform { get; } = PlatformUtility.GetOperatingSystem() switch
        {
            PlatformID.Win32NT => "windows-latest",
            PlatformID.Unix => "ubuntu-latest",
            PlatformID.MacOSX => "macos-latest",
            // ReSharper disable once ThrowExceptionInUnexpectedLocation
            _ => throw new NotSupportedException("Unsupported operating system."),
        };

        /// <summary>
        ///     Start the auto update service.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_option.Enabled) return Task.CompletedTask;
            // ReSharper disable once AsyncVoidFunctionExpression
            _timer = new(async void (_) => await CheckUpdateAsync().ConfigureAwait(false), null, TimeSpan.Zero,
                TimeSpan.FromMilliseconds(_option.CheckInterval));
            logger.LogInformation("Auto update service started");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stop the auto update service.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_option.Enabled) return;
            if (_timer != null) await _timer.DisposeAsync().ConfigureAwait(false);
            logger.LogInformation("Auto update service stopped");
        }

        private async Task CheckUpdateAsync()
        {
            if (!await CheckLoginAsync().ConfigureAwait(false)) return;
            var client = gitHubClientProviderService.Client;
            var runsResponse = await client.Actions.Workflows.Runs.List(RepositoryOwner, RepositoryName, new()
            {
                Branch = BranchName,
                Status = CheckRunStatusFilter.Success,
            }).ConfigureAwait(false);
            var latestRun = runsResponse.WorkflowRuns.ToArray().FirstOrDefault();
            if (latestRun is null) return;

            var jobsResponse = await client.Actions.Workflows.Jobs.List(RepositoryOwner, RepositoryName, latestRun.Id)
                .ConfigureAwait(false);
            var targetJob = jobsResponse.Jobs.FirstOrDefault(job => job.Name == TargetJobName);
            if (targetJob is null) return;

            var artifactsResponse = await client.Actions.Artifacts
                .ListWorkflowArtifacts(RepositoryOwner, RepositoryName, latestRun.Id).ConfigureAwait(false);
            var targetArtifact =
                artifactsResponse.Artifacts.FirstOrDefault(artifact =>
                    artifact.Name.EndsWith(TargetOsArtifactPlatform));
            if (targetArtifact is null) return;

            logger.LogDebug(
                "Latest run: {RunId}, Latest job: {JobId}, Created at: {CreatedAt}, Target artifact: {ArtifactName}",
                latestRun.Id, targetJob.Id, latestRun.CreatedAt, targetArtifact.Name);

            if (!CheckVersion(targetArtifact.Name))
            {
                logger.LogDebug("Not a newer version, skipping");
                return;
            }

            logger.LogDebug("Newer version found, downloading...");
            var artifactStream = await client.Actions.Artifacts
                .DownloadArtifact(RepositoryOwner, RepositoryName, targetArtifact.Id, "zip").ConfigureAwait(false);
            if (artifactStream is null)
            {
                logger.LogError("Failed to download artifact");
                return;
            }

            try
            {
                await UpdateAsync(artifactStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update");
            }
        }

        private async Task<bool> CheckLoginAsync()
        {
            try
            {
                await gitHubClientProviderService.Client.User.Current().ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task UpdateAsync(Stream stream)
        {
            logger.LogDebug("Saving update to update.zip...");
            const string updateZipPath = "update.zip";
            const string updateDirPath = "update";
            
            try
            {
                // Clean up any existing update files
                if (File.Exists(updateZipPath))
                {
                    File.Delete(updateZipPath);
                }
                if (Directory.Exists(updateDirPath))
                {
                    Directory.Delete(updateDirPath, true);
                }
                
                // Save the stream to update.zip
                await using var fileStream =
                    new FileStream(updateZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                stream.Close();
                fileStream.Close();

                logger.LogDebug("Extracting update...");
                
                // Create update directory
                Directory.CreateDirectory(updateDirPath);
                
                // Extract with overwrite enabled
                ZipFile.ExtractToDirectory(updateZipPath, updateDirPath, true);
                
                // Verify extraction succeeded
                if (!Directory.Exists(updateDirPath) || !Directory.GetFileSystemEntries(updateDirPath).Any())
                {
                    logger.LogError("Update extraction failed - update directory is empty");
                    return;
                }
                
                // Clean up the zip file
                File.Delete(updateZipPath);
                
                logger.LogDebug("Update extracted successfully. Files found: {FileCount}", 
                    Directory.GetFileSystemEntries(updateDirPath, "*", SearchOption.AllDirectories).Length);

                logger.LogDebug("Generating update script...");
                var scriptPath = await GenerateUpdateScriptAsync().ConfigureAwait(false);
                
                logger.LogDebug("Executing update script: {ScriptPath}", scriptPath);
                
                if (PlatformUtility.GetOperatingSystem() == PlatformID.Win32NT)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Environment.CurrentDirectory
                    });
                }
                else
                {
                    // Make script executable
                    Process.Start("chmod", $"+x {scriptPath}")?.WaitForExit();
                    Process.Start("sh", scriptPath);
                }
                
                // Give a moment for the script to start before cancelling
                await Task.Delay(1000).ConfigureAwait(false);
                HostCancellationToken.Cancel();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process update");
                
                // Clean up on failure
                try
                {
                    if (File.Exists(updateZipPath))
                        File.Delete(updateZipPath);
                    if (Directory.Exists(updateDirPath))
                        Directory.Delete(updateDirPath, true);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx, "Failed to clean up update files after error");
                }
                
                throw;
            }
        }

        private static async Task<string> GenerateUpdateScriptAsync()
        {
            var pid = Environment.ProcessId;
            var script = new StringBuilder();
            var assemblyName = Assembly.GetEntryAssembly()!.GetName().Name;
            if (PlatformUtility.GetOperatingSystem() == PlatformID.Win32NT)
            {
                script.AppendLine("@echo off");
                script.AppendLine($"title Updating {assemblyName}");
                script.AppendLine("echo Stopping current process...");
                script.AppendLine($"taskkill /pid {pid} /f");
                
                // Wait for process to fully terminate
                script.AppendLine("echo Waiting for process to terminate...");
                script.AppendLine("timeout /t 3 /nobreak > nul");
                
                // Wait until process is actually gone
                script.AppendLine(":waitloop");
                script.AppendLine($"tasklist /fi \"pid eq {pid}\" 2>nul | find \"{pid}\" >nul");
                script.AppendLine("if not errorlevel 1 (");
                script.AppendLine("    timeout /t 1 /nobreak > nul");
                script.AppendLine("    goto waitloop");
                script.AppendLine(")");
                
                script.AppendLine("echo Process terminated. Cleaning up old files...");
                
                // More comprehensive cleanup
                script.AppendLine("if exist libraries rmdir /s /q libraries");
                script.AppendLine("if exist runtimes rmdir /s /q runtimes");
                script.AppendLine("if exist ref rmdir /s /q ref");
                
                // Delete old executable files that might be locked
                script.AppendLine($"if exist {assemblyName}.exe del /f /q {assemblyName}.exe");
                script.AppendLine($"if exist {assemblyName}.dll del /f /q {assemblyName}.dll");
                script.AppendLine($"if exist {assemblyName}.pdb del /f /q {assemblyName}.pdb");
                
                script.AppendLine("echo Copying new files...");
                
                // Use robocopy for better file handling
                script.AppendLine("robocopy update . /e /move /r:3 /w:1");
                script.AppendLine("if exist update rmdir /s /q update");
                
                script.AppendLine("echo Restarting application...");
                script.AppendLine($"start \"\" \"{assemblyName}.exe\"");
                script.AppendLine("del \"%~f0\"");

                await File.WriteAllTextAsync("update.bat", script.ToString()).ConfigureAwait(false);
                return "update.bat";
            }

            script.AppendLine("#!/bin/bash");
            script.AppendLine("echo Stopping current process...");
            script.AppendLine($"kill {pid}");
            
            // Wait for process to terminate
            script.AppendLine("echo Waiting for process to terminate...");
            script.AppendLine("sleep 3");
            
            // Wait until process is actually gone
            script.AppendLine($"while kill -0 {pid} 2>/dev/null; do");
            script.AppendLine("    echo Process still running, waiting...");
            script.AppendLine("    sleep 1");
            script.AppendLine("done");
            
            script.AppendLine("echo Process terminated. Cleaning up old files...");
            
            // More comprehensive cleanup
            script.AppendLine("rm -rf libraries runtimes ref");
            script.AppendLine($"rm -f {assemblyName} {assemblyName}.dll {assemblyName}.pdb");
            
            script.AppendLine("echo Copying new files...");
            
            // Better file copying with error handling
            script.AppendLine("if [ -d \"update\" ]; then");
            script.AppendLine("    cp -rf update/* . 2>/dev/null || echo 'Some files could not be copied'");
            script.AppendLine("    rm -rf update");
            script.AppendLine("else");
            script.AppendLine("    echo 'Error: update directory not found'");
            script.AppendLine("    exit 1");
            script.AppendLine("fi");
            
            script.AppendLine("echo Setting permissions and restarting...");
            script.AppendLine($"chmod +x {assemblyName}");
            script.AppendLine($"nohup ./{assemblyName} > /dev/null 2>&1 &");
            script.AppendLine("rm -- \"$0\"");

            await File.WriteAllTextAsync("update.sh", script.ToString()).ConfigureAwait(false);
            return "update.sh";
        }

        private static bool CheckVersion(string artifactName)
        {
            var match = VersionRegex().Match(artifactName);
            if (!match.Success) return false;

            var artifactVersion = match.Value;
            const string currentVersion = "1.0.0"; // GitVersionInformation.FullSemVer!;

            return new Version(artifactVersion.Replace('-', '.')) > new Version(currentVersion.Replace('-', '.'));
        }

        [GeneratedRegex(@"[\d.]+-[\d]+", RegexOptions.Compiled)]
        private static partial Regex VersionRegex();
    }
}