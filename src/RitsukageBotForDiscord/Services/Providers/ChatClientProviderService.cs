using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.AI.Attributes;
using AIFunction = Microsoft.Extensions.AI.AIFunction;
using AIFunctionFactory = Microsoft.Extensions.AI.AIFunctionFactory;
using AITool = Microsoft.Extensions.AI.AITool;
using ChatClientBuilder = Microsoft.Extensions.AI.ChatClientBuilder;
using IChatClient = Microsoft.Extensions.AI.IChatClient;
using OllamaChatClient = Microsoft.Extensions.AI.OllamaChatClient;
using OpenAIChatClient = Microsoft.Extensions.AI.OpenAIChatClient;
using StreamingChatCompletionUpdate = Microsoft.Extensions.AI.StreamingChatCompletionUpdate;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Service to provide the chat client
    /// </summary>
    public class ChatClientProviderService
    {
        private readonly Dictionary<Assembly, List<AIFunction>> _bundleRecords = [];
        private readonly IChatClient? _chatClient;
        private readonly bool _isEnabled;
        private readonly ILogger<ChatClientProviderService> _logger;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="serviceProvider"></param>
        public ChatClientProviderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<ChatClientProviderService>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _isEnabled = configuration.GetValue<bool>("AI:Enabled");
            if (!_isEnabled)
            {
                _logger.LogInformation("Chat client is disabled");
                return;
            }

            var endpoint = configuration.GetValue<string>("AI:Endpoint");
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogError("Chat client endpoint is not set, chat client is disabled");
                return;
            }

            var modelId = configuration.GetValue<string>("AI:ModelId");
            if (string.IsNullOrEmpty(modelId))
            {
                _logger.LogError("Chat client model id is not set, chat client is disabled");
                return;
            }

            var key = configuration.GetValue<string>("AI:ApiKey");
            IChatClient innerChatClient = string.IsNullOrEmpty(key)
                ? new OllamaChatClient(new Uri(endpoint), modelId)
                : new OpenAIChatClient(new(new(key), new()
                {
                    Endpoint = new(endpoint),
                }), modelId);
            _chatClient = new ChatClientBuilder(innerChatClient).UseFunctionInvocation()
                .UseDistributedCache(serviceProvider.GetRequiredService<IDistributedCache>())
                //.UseLogging(serviceProvider.GetRequiredService<ILoggerFactory>())
                .Build();
            _logger.LogInformation("Chat client is enabled");
            RegisterChatClientToolsBundle(Assembly.GetExecutingAssembly());
        }

        /// <summary>
        ///     Check if the chat client is enabled
        /// </summary>
        /// <returns></returns>
        public bool IsEnabled()
        {
            return _isEnabled;
        }

        /// <summary>
        ///     Get chat client
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IChatClient GetChatClient()
        {
            return _chatClient ?? throw new InvalidOperationException("Chat client is not enabled");
        }

        /// <summary>
        ///     Register chat client tools bundle from assembly
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public int RegisterChatClientToolsBundle(Assembly assembly)
        {
            _logger.LogDebug("Registering chat client tools bundle from assembly: {Assembly}", assembly.FullName);
            var moduleBaseTypes = assembly.GetTypes()
                .Where(x => x.GetCustomAttribute<ChatClientToolsBundleAttribute>() != null)
                .ToArray();
            if (moduleBaseTypes.Length == 0)
            {
                _logger.LogDebug("No chat client tools bundle found in assembly: {Assembly}", assembly.FullName);
                return 0;
            }

            var count = 0;
            var functions = new List<AIFunction>();
            foreach (var type in moduleBaseTypes)
            {
                var c = CheckConstructor(type);
                if (c < 0)
                {
                    _logger.LogError("Failed to find a valid constructor for type: {Type}", type);
                    continue;
                }

                var methods = type.GetMethods()
                    .Where(x => x.GetCustomAttribute<ChatClientToolAttribute>() != null)
                    .ToArray();
                if (methods.Length == 0)
                    continue;

                object? target = null;
                try
                {
                    target = c switch
                    {
                        0 => Activator.CreateInstance(type),
                        1 => Activator.CreateInstance(type, _serviceProvider),
                        _ => null,
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create instance of type: {Type}", type);
                }

                if (target == null)
                    continue;

                foreach (var method in methods)
                {
                    var function = method.IsStatic
                        ? AIFunctionFactory.Create(method, null)
                        : AIFunctionFactory.Create(method, target);
                    functions.Add(function);
                    _logger.LogDebug("Registered chat client tool: {Function}", function);
                    count++;
                }
            }

            _bundleRecords[assembly] = functions;
            _logger.LogDebug("Registered {Count} chat client tools from assembly: {Assembly}", count,
                assembly.FullName);
            return count;
        }

        /// <summary>
        ///     Get response from chat client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="useTools"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(string message,
            bool useTools = false,
            CancellationToken cancellationToken = default)
        {
            var client = GetChatClient();
            return useTools
                ? client.CompleteStreamingAsync(message, new() { Tools = GetAiFunctions() },
                    cancellationToken)
                : client.CompleteStreamingAsync(message, cancellationToken: cancellationToken);
        }

        /// <summary>
        ///     Get response from chat client
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="useTools"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            bool useTools = false,
            CancellationToken cancellationToken = default)
        {
            var client = GetChatClient();
            return useTools
                ? client.CompleteStreamingAsync(messages, new() { Tools = GetAiFunctions() }, cancellationToken)
                : client.CompleteStreamingAsync(messages, cancellationToken: cancellationToken);
        }

        /// <summary>
        ///     Get response from chat client
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="stopSequences"></param>
        /// <param name="useTools"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            IList<string> stopSequences,
            bool useTools = false,
            CancellationToken cancellationToken = default)
        {
            var client = GetChatClient();
            var option = useTools ? new() { Tools = GetAiFunctions() } : new ChatOptions();
            option.StopSequences = stopSequences;
            return client.CompleteStreamingAsync(messages, option, cancellationToken);
        }

        /// <summary>
        ///     Get response from chat client
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="options"></param>
        /// <param name="useTools"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            Action<ChatOptions> options,
            bool useTools = false,
            CancellationToken cancellationToken = default)
        {
            var client = GetChatClient();
            var option = useTools ? new() { Tools = GetAiFunctions() } : new ChatOptions();
            options(option);
            return client.CompleteStreamingAsync(messages, option, cancellationToken);
        }

        private List<AITool>? GetAiFunctions()
        {
            if (_bundleRecords.Count != 0) return _bundleRecords.Values.SelectMany(x => x.Cast<AITool>()).ToList();
            _logger.LogWarning("No chat client tools bundle registered");
            return null;
        }

        private static int CheckConstructor(Type type)
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition || type.IsValueType) return -2;

            var constructors = type.GetConstructors();
            if (constructors.Length == 0) return -3;

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                switch (parameters.Length)
                {
                    case 0:
                        return 0;
                    case 1:
                        if (parameters[0].ParameterType == typeof(IServiceProvider))
                            return 1;
                        break;
                }
            }

            return -1;
        }
    }
}