using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
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
        private readonly List<IChatClient> _chatClients = [];
        private readonly IConfiguration _configuration;
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
            _configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _logger = serviceProvider.GetRequiredService<ILogger<ChatClientProviderService>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _isEnabled = configuration.GetValue<bool>("AI:Enabled");
            if (!_isEnabled)
            {
                _logger.LogInformation("Chat client is disabled");
                return;
            }

            var services = configuration.GetSection("AI:Service").Get<EndpointConfig[]>()
                ?.Where(x => !string.IsNullOrWhiteSpace(x.Endpoint) && !string.IsNullOrWhiteSpace(x.ModelId)).ToArray();
            if (services is null || services.Length == 0)
            {
                _logger.LogError("Chat client endpoint is not set, chat client is disabled");
                return;
            }

            foreach (var service in services)
            {
                IChatClient innerChatClient;
                if (string.IsNullOrWhiteSpace(service.ApiKey))
                    innerChatClient = new OllamaChatClient(new Uri(service.Endpoint), service.ModelId);
                else
                    innerChatClient = new OpenAIChatClient(new(new(service.ApiKey), new()
                    {
                        Endpoint = new(service.Endpoint),
                    }), service.ModelId);

                var client = new ChatClientBuilder(innerChatClient).UseFunctionInvocation()
                    .UseDistributedCache(serviceProvider.GetRequiredService<IDistributedCache>())
                    //.UseLogging(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .Build();
                _chatClients.Add(client);
            }

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
            if (_chatClients.Count == 0 || !_isEnabled)
                throw new InvalidOperationException("Chat client is not enabled");
            return _chatClients[0];
        }

        /// <summary>
        ///     Get chat client
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IChatClient GetChatClientRandomly()
        {
            if (_chatClients.Count == 0 || !_isEnabled)
                throw new InvalidOperationException("Chat client is not enabled");
            return _chatClients[Random.Shared.Next(_chatClients.Count)];
        }

        /// <summary>
        ///     Get chat client
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IChatClient[] GetChatClients()
        {
            if (_chatClients.Count == 0 || !_isEnabled)
                throw new InvalidOperationException("Chat client is not enabled");
            return _chatClients.ToArray();
        }

        /// <summary>
        ///     Get role data
        /// </summary>
        /// <returns></returns>
        public bool GetRoleData([NotNullWhen(true)] out ChatMessage? chatMessage, out float temperature,
            string type = "Normal")
        {
            chatMessage = null;
            temperature = 1.0f;
            var roleData = _configuration.GetSection($"AI:RoleData:{type}").Get<RoleConfig>();
            if (roleData is null)
            {
                _logger.LogWarning("Role {Type} data not found", type);
                return false;
            }

            temperature = roleData.Temperature;
            var prompt = roleData.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                if (string.IsNullOrWhiteSpace(roleData.PromptFile))
                {
                    _logger.LogWarning("Role {Type} has no prompt or prompt file", type);
                    return false;
                }

                if (!File.Exists(roleData.PromptFile))
                {
                    _logger.LogWarning("Role {Type} prompt file does not exist", type);
                    return false;
                }

                prompt = File.ReadAllText(roleData.PromptFile);
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("Role {Type} prompt is empty", type);
                return false;
            }

            var chatRoleString = _configuration.GetValue<string>("AI:PromptRole", "System");
            var chatRole = chatRoleString.ToLower() switch
            {
                "system" => ChatRole.System,
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                "tool" => ChatRole.Tool,
#pragma warning disable CA2208
                _ => throw new ArgumentOutOfRangeException(nameof(chatRoleString), "Invalid chat role"),
#pragma warning restore CA2208
            };
            chatMessage = new(chatRole, prompt);
            return true;
        }

        /// <summary>
        ///     Get roles
        /// </summary>
        /// <returns></returns>
        public string[] GetRoles()
        {
            return _configuration.GetSection("AI:RoleData").GetChildren().Select(x => x.Key).ToArray();
        }

        /// <summary>
        ///     Check message header
        /// </summary>
        /// <param name="response"></param>
        /// <param name="content"></param>
        /// <param name="jsonHeader"></param>
        /// <returns></returns>
        public static bool CheckJsonHeader(string response, out string content, out string? jsonHeader)
        {
            response = response.Trim();
            if (response is not ['{', ..])
            {
                content = response;
                jsonHeader = null;
                return false;
            }

            var firstLineEndIndex = response.IndexOf('\n');
            if (firstLineEndIndex == -1)
                firstLineEndIndex = response.IndexOf('\r');
            if (firstLineEndIndex == -1)
            {
                content = response;
                jsonHeader = null;
                return false;
            }

            var firstLine = response[..firstLineEndIndex].Trim();
            response = response[(firstLineEndIndex + 1)..].Trim();
            content = response;
            jsonHeader = firstLine;
            return true;
        }

        /// <summary>
        ///     Format response
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public static (bool, string, string?, string?) FormatResponse(string response)
        {
            response = response.Trim();

            if (!response.StartsWith("<think>"))
            {
                if (CheckJsonHeader(response, out var content, out var jsonHeader))
                    return (true, content, jsonHeader, null);
                return (false, response, null, null);
            }

            {
                string thinkContent;
                var hasJsonHeader = false;
                string? jsonHeader = null;
                var content = string.Empty;

                var thinkEndIndex = response.IndexOf("</think>", StringComparison.Ordinal);
                if (thinkEndIndex != -1)
                {
                    thinkContent = response[7..thinkEndIndex];
                    content = response[(thinkEndIndex + 8)..];
                }
                else
                {
                    thinkContent = response[7..];
                }

                var lines = thinkContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    sb.Append("> ");
                    sb.AppendLine(line);
                }

                if (string.IsNullOrWhiteSpace(content)) return (hasJsonHeader, string.Empty, jsonHeader, sb.ToString());
                hasJsonHeader = CheckJsonHeader(content, out content, out jsonHeader);
                return (hasJsonHeader, content, jsonHeader, sb.ToString());
            }
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
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(string message,
            bool useTools = false,
            IChatClient? client = null,
            CancellationToken cancellationToken = default)
        {
            client ??= GetChatClients()[0];
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
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            bool useTools = false,
            IChatClient? client = null,
            CancellationToken cancellationToken = default)
        {
            client ??= GetChatClients()[0];
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
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            IList<string> stopSequences,
            bool useTools = false,
            IChatClient? client = null,
            CancellationToken cancellationToken = default)
        {
            client ??= GetChatClients()[0];
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
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> messages,
            Action<ChatOptions> options,
            bool useTools = false,
            IChatClient? client = null,
            CancellationToken cancellationToken = default)
        {
            client ??= GetChatClients()[0];
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

        internal record EndpointConfig(string Endpoint, string ApiKey, string ModelId);

        internal record RoleConfig(string Prompt, string PromptFile, float Temperature);
    }
}