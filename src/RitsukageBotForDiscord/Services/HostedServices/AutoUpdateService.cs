using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Octokit;
using RitsukageBot.Library.Utils;
using RitsukageBot.Options;
using RitsukageBot.Services.Providers;
using FileMode = System.IO.FileMode;

namespace RitsukageBot.Services.HostedServices
{
    /// <summary>
    ///     Auto update service.
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
            _ => throw new NotSupportedException("Unsupported operating system."),
        };

        /// <summary>
        ///     Start the auto update service.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_option.Enable) return Task.CompletedTask;
            _timer = new(async void (_) => await CheckUpdateAsync().ConfigureAwait(false), null, TimeSpan.Zero,
                TimeSpan.FromMilliseconds(_option.CheckInterval));
            logger.LogInformation("Auto update service started.");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stop the auto update service.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_option.Enable) return;
            if (_timer != null) await _timer.DisposeAsync().ConfigureAwait(false);
            logger.LogInformation("Auto update service stopped.");
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

            logger.LogInformation(
                "Latest run: {RunId}, Latest job: {JobId}, Created at: {CreatedAt}, Target artifact: {ArtifactName}",
                latestRun.Id, targetJob.Id, latestRun.CreatedAt, targetArtifact.Name);

            if (!CheckVersion(targetArtifact.Name))
            {
                logger.LogInformation("Not a newer version, skipping.");
                return;
            }

            logger.LogInformation("Newer version found, downloading...");
            var artifactStream = await client.Actions.Artifacts
                .DownloadArtifact(RepositoryOwner, RepositoryName, targetArtifact.Id, "zip").ConfigureAwait(false);
            if (artifactStream is null)
            {
                logger.LogError("Failed to download artifact.");
                return;
            }

            try
            {
                await UpdateAsync(artifactStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update.");
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

        private static async Task UpdateAsync(Stream stream)
        {
            await using var fileStream =
                new FileStream("update.zip", FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            stream.Close();
            fileStream.Close();

            ZipFile.ExtractToDirectory("update.zip", "update", true);
            File.Delete("update.zip");

            var appSettingsPath = Path.Combine("update", "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var appSettings = JObject.Parse(await File.ReadAllTextAsync(appSettingsPath).ConfigureAwait(false));
                var currentAppSettings =
                    JObject.Parse(await File.ReadAllTextAsync("appsettings.json").ConfigureAwait(false));
                var combineAppSettings = CombineAppSettings(currentAppSettings, appSettings);
                await File.WriteAllTextAsync(appSettingsPath, combineAppSettings.ToString()).ConfigureAwait(false);
            }

            var scriptPath = await GenerateUpdateScriptAsync().ConfigureAwait(false);
            if (PlatformUtility.GetOperatingSystem() == PlatformID.Win32NT)
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                });
            else
                Process.Start("sh", scriptPath);
        }

        private static JToken CombineAppSettings(JToken current, JToken update)
        {
            if (current.Type != update.Type) return update;
            if (update.Type is not (JTokenType.Object or JTokenType.Array)) return update;
            if (update.Type is JTokenType.Array)
            {
                if (!update.Any()) return current;

                var flag = false;
                var tmpArray = new JArray();
                for (var i = 0; i < update.Count(); i++)
                {
                    var updateItem = update[i]!;
                    if (current.Count() <= i) break;
                    var currentItem = current[i]!;
                    if (updateItem.Type != currentItem.Type)
                    {
                        flag = true;
                        break;
                    }

                    if (updateItem.Type is JTokenType.Object or JTokenType.Array)
                        tmpArray[i] = CombineAppSettings(currentItem, updateItem);
                    else
                        tmpArray[i] = currentItem;
                }

                return flag ? update : tmpArray;
            }

            if (!update.Any()) return current;

            var tmpObject = new JObject();
            foreach (var updateProperty in update)
            {
                var currentProperty = current[updateProperty.Path]!;
                if (currentProperty.Type != updateProperty.Type)
                {
                    tmpObject[updateProperty.Path] = updateProperty;
                    continue;
                }

                tmpObject[updateProperty.Path] = updateProperty.Type is JTokenType.Object or JTokenType.Array
                    ? CombineAppSettings(currentProperty, updateProperty)
                    : currentProperty;
            }

            return tmpObject;
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