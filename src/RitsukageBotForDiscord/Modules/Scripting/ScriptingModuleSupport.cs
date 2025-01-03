using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Scripting;
using RitsukageBot.Modules.Commands;
using RitsukageBot.Modules.Interactions;
using RitsukageBot.Services;

namespace RitsukageBot.Modules.Scripting
{
    internal sealed class ScriptingModuleSupport(DiscordBotService discordBotService, IServiceProvider services) : IDiscordBotModule
    {
        public const string TagScriptModulePath = "ModuleScripts";
        public const string TagCommandModulePath = "Commands";
        public const string TagInteractionModulePath = "Interactions";
        private readonly CommandService _command = services.GetRequiredService<CommandService>();
        private readonly Dictionary<ScriptRuntime.AssemblyInfo, List<Type>> _commandModules = [];
        private readonly CommandModuleSupport _commandModuleSupport = new(discordBotService, services);

        private readonly List<Type> _commandModuleSupportBaseType = [typeof(ModuleBase<SocketCommandContext>)];
        private readonly InteractionService _interaction = services.GetRequiredService<InteractionService>();
        private readonly Dictionary<ScriptRuntime.AssemblyInfo, List<Type>> _interactionModules = [];
        private readonly InteractionModuleSupport _interactionModuleSupport = new(discordBotService, services);

        private readonly List<Type> _interactionModuleSupportBaseType =
        [
            typeof(InteractionModuleBase<SocketInteractionContext>),
            typeof(InteractionModuleBase<SocketInteractionContext<SocketModal>>),
            typeof(InteractionModuleBase<SocketInteractionContext<SocketUserCommand>>),
            typeof(InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>),
            typeof(InteractionModuleBase<SocketInteractionContext<SocketMessageCommand>>),
            typeof(InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>),
        ];

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
            if (!Directory.Exists(TagScriptModulePath))
            {
                _logger.LogDebug("Script module path not found: {path}", TagScriptModulePath);
                return;
            }

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

                    var commandModuleBaseType = assemblyInfo.Assembly.GetTypes().Where(x => x.BaseType != null && _commandModuleSupportBaseType.Contains(x.BaseType)).ToArray();
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
                        _logger.LogDebug("Loaded command module type: {type}", type);
                    }

                    _commandModules.Add(assemblyInfo, loadedTypes);
                    _logger.LogDebug("Loaded command module: {directory}", directoryName);
                }
                catch (CompilationErrorException ex)
                {
                    foreach (var diagnostic in ex.Diagnostics)
                    {
                        _logger.LogError("[{tag}][{source}] {diagnostic}", TagCommandModulePath, directoryName, diagnostic);
                    }

                    _logger.LogError(ex, "Failed to load command module: {directory}", directoryName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load command module: {directory}", directoryName);
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

                    var interactionModuleBaseType = assemblyInfo.Assembly.GetTypes().Where(x => x.BaseType != null && _interactionModuleSupportBaseType.Contains(x.BaseType)).ToArray();
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
                        _logger.LogDebug("Loaded interaction module type: {type}", type);
                    }

                    _interactionModules.Add(assemblyInfo, loadedTypes);
                    _logger.LogDebug("Loaded interaction module: {directory}", directoryName);
                }
                catch (CompilationErrorException ex)
                {
                    foreach (var diagnostic in ex.Diagnostics)
                    {
                        _logger.LogError("[{tag}][{source}] {diagnostic}", TagCommandModulePath, directoryName, diagnostic);
                    }

                    _logger.LogError(ex, "Failed to load command module: {directory}", directoryName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load interaction module: {directory}", directoryName);
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
                    _logger.LogDebug("Unloaded command module type: {type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded command module: {assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    _interaction.RemoveModuleAsync(type);
                    _logger.LogDebug("Unloaded interaction module type: {type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded interaction module: {assembly}", assemblyInfo.Name);
            }
        }

        public async Task UnloadScriptsAsync()
        {
            foreach (var (assemblyInfo, types) in _commandModules)
            {
                foreach (var type in types)
                {
                    await _command.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogDebug("Unloaded command module type: {type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded command module: {assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    await _interaction.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogDebug("Unloaded interaction module type: {type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded interaction module: {assembly}", assemblyInfo.Name);
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