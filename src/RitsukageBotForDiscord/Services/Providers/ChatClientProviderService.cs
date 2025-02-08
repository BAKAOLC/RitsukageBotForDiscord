using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChatClientBuilder = Microsoft.Extensions.AI.ChatClientBuilder;
using IChatClient = Microsoft.Extensions.AI.IChatClient;
using OllamaChatClient = Microsoft.Extensions.AI.OllamaChatClient;
using OpenAIChatClient = Microsoft.Extensions.AI.OpenAIChatClient;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Service to provide the chat client
    /// </summary>
    public class ChatClientProviderService
    {
        private readonly List<IChatClient> _chatClients = [];
        private readonly IConfiguration _configuration;
        private readonly bool _isEnabled;
        private readonly ILogger<ChatClientProviderService> _logger;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="serviceProvider"></param>
        public ChatClientProviderService(IServiceProvider serviceProvider)
        {
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

                var client = new ChatClientBuilder(innerChatClient)
                    .UseDistributedCache(serviceProvider.GetRequiredService<IDistributedCache>())
                    //.UseLogging(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .Build();
                _chatClients.Add(client);
            }

            _logger.LogInformation("Chat client is enabled");
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

        internal EndpointConfig[] GetEndpointConfigs()
        {
            return _configuration.GetSection("AI:Service").Get<EndpointConfig[]>() ?? [];
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
        public static bool CheckJsonHeader(string response, out string content,
            [NotNullWhen(true)] out string? jsonHeader)
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

        internal record EndpointConfig(string Endpoint, string ApiKey, string ModelId);

        internal record RoleConfig(string Prompt, string PromptFile, float Temperature);
    }
}