using Discord.Commands;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Scripting;
using RitsukageBot.Modules.Command;
using RitsukageBot.Modules.Interaction;
using RitsukageBot.Services;

namespace RitsukageBot.Modules.Scripting
{
    internal sealed class ScriptingModuleSupport(DiscordBotService discordBotService, IServiceProvider services) : IDiscordBotModule
    {
        public const string TagScriptModulePath = "Scripts";
        public const string TagCommandModulePath = "Commands";
        public const string TagInteractionModulePath = "Interactions";

        private readonly CommandService _command = services.GetRequiredService<CommandService>();

        private readonly Dictionary<ScriptRuntime.AssemblyInfo, List<Type>> _commandModules = [];
        private readonly CommandModuleSupport _commandModuleSupport = new(discordBotService, services);
        private readonly InteractionService _interaction = services.GetRequiredService<InteractionService>();
        private readonly Dictionary<ScriptRuntime.AssemblyInfo, List<Type>> _interactionModules = [];
        private readonly InteractionModuleSupport _interactionModuleSupport = new(discordBotService, services);
        private readonly ILogger<ScriptingModuleSupport> _logger = services.GetRequiredService<ILogger<ScriptingModuleSupport>>();

        public async Task InitAsync()
        {
            await _commandModuleSupport.InitAsync();
            await _interactionModuleSupport.InitAsync();
            await LoadScriptsAsync();
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public async Task LoadScriptsAsync()
        {
            foreach (var directory in Directory.GetDirectories(Path.Combine(TagScriptModulePath, TagCommandModulePath)))
            {
                var directoryName = Path.GetFileName(directory);
                var scriptFile = Path.Combine(directory, $"{directoryName}.cs");
                if (!File.Exists(scriptFile)) continue;
                try
                {
                    var script = await File.ReadAllTextAsync(scriptFile);
                    var scriptRuntime = ScriptRuntime.Create(script);
                    var (assemblyInfo, diagnostics) = scriptRuntime.CompileToAssembly(directoryName);
                    if (diagnostics.Any())
                    {
                        foreach (var diagnostic in diagnostics)
                        {
                            _logger.LogError("[{tag}][{source}] {diagnostic}", TagCommandModulePath, directoryName, diagnostic);
                        }

                        continue;
                    }

                    var commandModuleBaseType = assemblyInfo.Assembly.GetTypes().Where(x => x.BaseType == typeof(ModuleBase<SocketCommandContext>)).ToArray();
                    if (commandModuleBaseType.Length == 0)
                    {
                        _logger.LogError("Failed to find command module base type: {directory}", directoryName);
                        continue;
                    }

                    var loadedTypes = new List<Type>();
                    foreach (var type in commandModuleBaseType)
                    {
                        await _command.AddModuleAsync(type, services).ConfigureAwait(false);
                        loadedTypes.Add(type);
                        _logger.LogInformation("Loaded command module type: {type}", type);
                    }

                    _commandModules.Add(assemblyInfo, loadedTypes);
                    _logger.LogInformation("Loaded command module: {directory}", directoryName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            foreach (var directory in Directory.GetDirectories(Path.Combine(TagScriptModulePath, TagInteractionModulePath)))
            {
                var directoryName = Path.GetFileName(directory);
                var scriptFile = Path.Combine(directory, $"{directoryName}.cs");
                if (!File.Exists(scriptFile)) continue;
                try
                {
                    var script = await File.ReadAllTextAsync(scriptFile);
                    var scriptRuntime = ScriptRuntime.Create(script);
                    var (assemblyInfo, diagnostics) = scriptRuntime.CompileToAssembly(directoryName);
                    if (diagnostics.Any())
                    {
                        foreach (var diagnostic in diagnostics)
                        {
                            _logger.LogError("[{tag}][{source}] {diagnostic}", TagInteractionModulePath, directoryName, diagnostic);
                        }

                        continue;
                    }

                    var interactionModuleBaseType = assemblyInfo.Assembly.GetTypes().Where(x => x.BaseType == typeof(InteractionModuleBase<SocketInteractionContext>)).ToArray();
                    if (interactionModuleBaseType.Length == 0)
                    {
                        _logger.LogError("Failed to find interaction module base type: {directory}", directoryName);
                        continue;
                    }

                    var loadedTypes = new List<Type>();
                    foreach (var type in interactionModuleBaseType)
                    {
                        await _interaction.AddModuleAsync(type, services).ConfigureAwait(false);
                        loadedTypes.Add(type);
                        _logger.LogInformation("Loaded interaction module type: {type}", type);
                    }

                    _interactionModules.Add(assemblyInfo, loadedTypes);
                    _logger.LogInformation("Loaded interaction module: {directory}", directoryName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public void UnloadScripts()
        {
            foreach (var (assemblyInfo, types) in _commandModules)
            {
                foreach (var type in types)
                {
                    _command.RemoveModuleAsync(type);
                    _logger.LogInformation("Unloaded command module type: {type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogInformation("Unloaded command module: {assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    _interaction.RemoveModuleAsync(type);
                    _logger.LogInformation("Unloaded interaction module type: {type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogInformation("Unloaded interaction module: {assembly}", assemblyInfo.Name);
            }
        }

        public async Task UnloadScriptsAsync()
        {
            foreach (var (assemblyInfo, types) in _commandModules)
            {
                foreach (var type in types)
                {
                    await _command.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogInformation("Unloaded command module type: {type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogInformation("Unloaded command module: {assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    await _interaction.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogInformation("Unloaded interaction module type: {type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogInformation("Unloaded interaction module: {assembly}", assemblyInfo.Name);
            }
        }

        ~ScriptingModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            UnloadScripts();
            _commandModuleSupport.Dispose();
            _interactionModuleSupport.Dispose();
        }

        private async ValueTask DisposeAsyncCore()
        {
            await UnloadScriptsAsync().ConfigureAwait(false);
            await _commandModuleSupport.DisposeAsync().ConfigureAwait(false);
            await _interactionModuleSupport.DisposeAsync().ConfigureAwait(false);
        }
    }
}