using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
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
        private readonly DatabaseProviderService _databaseProviderService;
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
            _databaseProviderService = serviceProvider.GetRequiredService<DatabaseProviderService>();
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
        ///     Get prompt extensions
        /// </summary>
        /// <returns></returns>
        public string GetPromptExtensions()
        {
            var extensions = _configuration.GetSection("AI:PromptExtension").Get<PromptExtensionConfig[]>();
            if (extensions is null || extensions.Length == 0)
            {
                _logger.LogWarning("Prompt extensions not found");
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var extension in extensions)
            {
                var prompt = extension.Prompt;
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    if (string.IsNullOrWhiteSpace(extension.PromptFile))
                    {
                        _logger.LogWarning("Prompt extension has no prompt or prompt file");
                        continue;
                    }

                    if (!File.Exists(extension.PromptFile))
                    {
                        _logger.LogWarning("Prompt extension prompt file does not exist");
                        continue;
                    }

                    prompt = File.ReadAllText(extension.PromptFile);
                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        _logger.LogWarning("Prompt extension prompt is empty");
                        continue;
                    }
                }

                sb.AppendLine(prompt).AppendLine();
            }

            return sb.ToString().Trim();
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
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    _logger.LogWarning("Role {Type} prompt is empty", type);
                    return false;
                }
            }

            prompt = new StringBuilder(prompt).AppendLine().Append(GetPromptExtensions()).ToString().Trim();

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
        ///     Get memory
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<JObject> GetMemory(ulong userId, ChatMemoryType type = ChatMemoryType.ShortTerm)
        {
            var table = _databaseProviderService.Table<ChatMemory>();
            var memory = await table.Where(x => x.UserId == userId && x.Type == type)
                .OrderBy(x => x.Timestamp)
                .ToArrayAsync().ConfigureAwait(false);

            if (memory.Length == 0) return new();

            var data = new JObject();
            foreach (var item in memory)
                if (data.TryGetValue(item.Key, out var value))
                {
                    if (value is JArray { Count: > 0 } array)
                        array.Add(item.Value);
                    else
                        data[item.Key] = new JArray { value, item.Value };
                }
                else
                {
                    data[item.Key] = item.Value;
                }

            return data;
        }

        /// <summary>
        ///     Insert memory
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task InsertMemory(ulong userId, ChatMemoryType type, string key, string value)
        {
            var memory = new ChatMemory
            {
                UserId = userId,
                Type = type,
                Key = key,
                Value = value,
                Timestamp = DateTimeOffset.Now,
            };
            await _databaseProviderService.InsertOrUpdateAsync(memory).ConfigureAwait(false);
            _logger.LogInformation("Added new {Type} memory for {UserId} with key {Key}: {Value}", type, userId, key,
                value);
        }

        /// <summary>
        ///     Remove memory
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task RemoveMemory(ulong userId, ChatMemoryType type, string key)
        {
            var table = _databaseProviderService.Table<ChatMemory>();
            var memory = await table.Where(x => x.UserId == userId && x.Type == type && x.Key == key)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (memory is not null)
            {
                await _databaseProviderService.DeleteAsync(memory).ConfigureAwait(false);
                _logger.LogInformation("Removed {Type} memory for {UserId} with key {Key}: {Value}", type, userId, key,
                    memory.Value);
            }
        }

        /// <summary>
        ///     Refresh short memory
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public async Task RefreshShortMemory(ulong userId, int length = 2000)
        {
            var table = _databaseProviderService.Table<ChatMemory>();
            var memoryList = await table.Where(x => x.UserId == userId && x.Type == ChatMemoryType.ShortTerm)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync().ConfigureAwait(false);
            var totalLength = memoryList.Sum(x => x.Value.Length);
            while (totalLength > length)
            {
                var memory = memoryList[^1];
                memoryList.RemoveAt(memoryList.Count - 1);
                totalLength -= memory.Value.Length;
                await _databaseProviderService.DeleteAsync(memory).ConfigureAwait(false);
                _logger.LogInformation("Removed {Type} memory for {UserId} with key {Key}: {Value}",
                    ChatMemoryType.ShortTerm,
                    userId, memory.Key, memory.Value);
            }
        }

        /// <summary>
        ///     Build user chat message
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="time"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<ChatMessage?> BuildUserChatMessage(string name, ulong id, DateTimeOffset time,
            string message)
        {
            var (_, userInfo) = await _databaseProviderService.GetOrCreateAsync<ChatUserInformation>(id)
                .ConfigureAwait(false);
            var shortMemory = await GetMemory(id).ConfigureAwait(false);
            var longMemory = await GetMemory(id, ChatMemoryType.LongTerm).ConfigureAwait(false);
            var jObject = new JObject
            {
                ["name"] = name,
                ["message"] = message,
                ["data"] = new JObject
                {
                    ["short_memory"] = shortMemory,
                    ["long_memory"] = longMemory,
                    ["good"] = userInfo.Good,
                    ["time"] = time.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                },
            };
            return new(ChatRole.User, jObject.ToString());
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
            if (response is not ['{', ..] or ['[', ..])
            {
                content = response;
                jsonHeader = null;
                return false;
            }

            var depth = new Stack<char>();
            var jsonStringBuilder = new StringBuilder();
            foreach (var c in response)
            {
                switch (c)
                {
                    case '{' or '[':
                        depth.Push(c);
                        break;
                    case '}' or ']' when depth.Count == 0:
                        content = response;
                        jsonHeader = null;
                        return false;
                    case '}' or ']':
                        depth.Pop();
                        break;
                }

                jsonStringBuilder.Append(c);
                if (depth.Count == 0)
                    break;
            }

            if (depth.Count != 0)
            {
                content = string.Empty;
                jsonHeader = null;
                return false;
            }

            if (jsonStringBuilder.Length == 0)
            {
                content = response;
                jsonHeader = null;
                return false;
            }

            content = response[(jsonStringBuilder.Length + 1)..].Trim();
            jsonHeader = jsonStringBuilder.ToString().Trim();
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

        internal record EndpointConfig(string Endpoint, string ModelId, string ApiKey = "");

        internal record PromptExtensionConfig(string Prompt = "", string PromptFile = "");

        internal record RoleConfig(string Prompt = "", string PromptFile = "", float Temperature = 0.6f);
    }
}