using System.Diagnostics;
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
            await using var fileStream =
                new FileStream("update.zip", FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            stream.Close();
            fileStream.Close();

            logger.LogDebug("Extracting update...");
            ZipFile.ExtractToDirectory("update.zip", "update", true);
            File.Delete("update.zip");

            logger.LogDebug("Generating update script...");
            var scriptPath = await GenerateUpdateScriptAsync().ConfigureAwait(false);
            logger.LogDebug("Executing update script...");
            if (PlatformUtility.GetOperatingSystem() == PlatformID.Win32NT)
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });
            else
                Process.Start("sh", scriptPath);
            HostCancellationToken.Cancel();
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
                script.AppendLine("echo Deleting libraries and runtimes folders...");
                script.AppendLine("rmdir /s /q libraries");
                script.AppendLine("rmdir /s /q runtimes");
                script.AppendLine("echo Done.");
                script.AppendLine("echo Moving update folder...");
                script.AppendLine("xcopy /e /y update .");
                script.AppendLine("rmdir /s /q update");
                script.AppendLine("echo Restarting...");
                script.AppendLine($"start {assemblyName}.exe");
                script.AppendLine("del %0");

                await File.WriteAllTextAsync("update.bat", script.ToString()).ConfigureAwait(false);
                return "update.bat";
            }

            script.AppendLine("#!/bin/bash");
            script.AppendLine("echo Stopping current process...");
            script.AppendLine($"kill {pid}");
            script.AppendLine("echo Deleting libraries and runtimes folders...");
            script.AppendLine("rm -rf libraries");
            script.AppendLine("rm -rf runtimes");
            script.AppendLine("echo Done.");
            script.AppendLine("echo Moving update folder...");
            script.AppendLine("cp -r update/* .");
            script.AppendLine("rm -rf update");
            script.AppendLine("echo Restarting...");
            script.AppendLine($"chmod +x {assemblyName}");
            script.AppendLine($"./{assemblyName} &");
            script.AppendLine("rm $0");

            await File.WriteAllTextAsync("update.sh", script.ToString()).ConfigureAwait(false);
            return "update.sh";
        }

        private static bool CheckVersion(string artifactName)
        {
            var match = VersionRegex().Match(artifactName);
            if (!match.Success) return false;

            var artifactVersion = match.Value;
            const string currentVersion = GitVersionInformation.FullSemVer!;

            return new Version(artifactVersion.Replace('-', '.')) > new Version(currentVersion.Replace('-', '.'));
        }

        [GeneratedRegex(@"[\d.]+-[\d]+", RegexOptions.Compiled)]
        private static partial Regex VersionRegex();
    }
}