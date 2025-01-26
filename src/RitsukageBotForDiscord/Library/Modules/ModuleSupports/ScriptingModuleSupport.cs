using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Scripting;
using RitsukageBot.Services.HostedServices;

namespace RitsukageBot.Library.Modules.ModuleSupports
{
    internal sealed class ScriptingModuleSupport(DiscordBotService discordBotService, IServiceProvider services)
        : IDiscordBotModule
    {
        public const string ScriptModulePath = "ModuleScripts";
        public const string CommandModulePath = "Commands";
        public const string InteractionModulePath = "Interactions";
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

        private readonly ILogger<ScriptingModuleSupport> _logger =
            services.GetRequiredService<ILogger<ScriptingModuleSupport>>();

        public async Task InitAsync()
        {
            await _commandModuleSupport.InitAsync().ConfigureAwait(false);
            await _interactionModuleSupport.InitAsync().ConfigureAwait(false);
            await LoadScriptsAsync().ConfigureAwait(false);
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
            if (!Directory.Exists(ScriptModulePath))
            {
                _logger.LogWarning("Script module path not found: {Path}", ScriptModulePath);
                _logger.LogWarning("Skip loading scripts");
                return;
            }

            await LoadModulesAsync(CommandModulePath, _commandModuleSupportBaseType, _command, _commandModules).ConfigureAwait(false);
            await LoadModulesAsync(InteractionModulePath, _interactionModuleSupportBaseType, _interaction, _interactionModules).ConfigureAwait(false);
        }

        private async Task LoadModulesAsync(string modulePath, IEnumerable<Type> baseTypes, dynamic service, Dictionary<ScriptRuntime.AssemblyInfo, List<Type>> modules)
        {
            foreach (var directory in Directory.GetDirectories(Path.Combine(ScriptModulePath, modulePath)))
            {
                var directoryName = Path.GetFileName(directory);
                var scriptFile = Path.Combine(directory, $"{directoryName}.cs");
                if (!File.Exists(scriptFile)) continue;

                try
                {
                    var script = await File.ReadAllTextAsync(scriptFile).ConfigureAwait(false);
                    var scriptRuntime = ScriptRuntime.Create(script);
                    var (assemblyInfo, diagnostics) = scriptRuntime.CompileToAssembly(directoryName);

                    if (diagnostics.Any())
                    {
                        LogDiagnostics(diagnostics, modulePath, directoryName);
                        continue;
                    }

                    var moduleBaseTypes = assemblyInfo.Assembly.GetTypes().Where(x => x.BaseType != null && baseTypes.Contains(x.BaseType)).ToArray();
                    if (moduleBaseTypes.Length == 0)
                    {
                        _logger.LogError("Failed to find module base type: {Directory}", directoryName);
                        continue;
                    }

                    var loadedTypes = new List<Type>();
                    foreach (var type in moduleBaseTypes)
                    {
                        await service.AddModuleAsync(type, services).ConfigureAwait(false);
                        loadedTypes.Add(type);
                        _logger.LogDebug("Loaded module type: {Type}", type);
                    }

                    modules.Add(assemblyInfo, loadedTypes);
                    _logger.LogDebug("Loaded module: {Directory}", directoryName);
                }
                catch (CompilationErrorException ex)
                {
                    LogDiagnostics(ex.Diagnostics, modulePath, directoryName);
                    _logger.LogError(ex, "Failed to load module: {Directory}", directoryName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load module: {Directory}", directoryName);
                }
            }
        }

        private void LogDiagnostics(IEnumerable<Diagnostic> diagnostics, string modulePath, string directoryName)
        {
            foreach (var diagnostic in diagnostics) _logger.LogError("[{Tag}][{Source}] {Diagnostic}", modulePath, directoryName, diagnostic);
        }

        public void UnloadScripts()
        {
            foreach (var (assemblyInfo, types) in _commandModules)
            {
                foreach (var type in types)
                {
                    _command.RemoveModuleAsync(type);
                    _logger.LogDebug("Unloaded command module type: {Type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded command module: {Assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    _interaction.RemoveModuleAsync(type);
                    _logger.LogDebug("Unloaded interaction module type: {Type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded interaction module: {Assembly}", assemblyInfo.Name);
            }
        }

        public async Task UnloadScriptsAsync()
        {
            foreach (var (assemblyInfo, types) in _commandModules)
            {
                foreach (var type in types)
                {
                    await _command.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogDebug("Unloaded command module type: {Type}", type);
                }

                _commandModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded command module: {Assembly}", assemblyInfo.Name);
            }

            foreach (var (assemblyInfo, types) in _interactionModules)
            {
                foreach (var type in types)
                {
                    await _interaction.RemoveModuleAsync(type).ConfigureAwait(false);
                    _logger.LogDebug("Unloaded interaction module type: {Type}", type);
                }

                _interactionModules.Remove(assemblyInfo);
                _logger.LogDebug("Unloaded interaction module: {Assembly}", assemblyInfo.Name);
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