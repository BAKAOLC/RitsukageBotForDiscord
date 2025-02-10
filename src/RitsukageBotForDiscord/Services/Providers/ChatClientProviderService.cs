using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
                try
                {
                    var client = CreateChatClient(service);
                    _chatClients.Add(client);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to create chat client for {Endpoint}", service.Endpoint);
                }

            if (_chatClients.Count == 0)
            {
                _logger.LogError("Failed to create chat client, chat client is disabled");
                _isEnabled = false;
                return;
            }

            _logger.LogInformation("Chat client is enabled");
        }

        private IChatClient CreateChatClient(EndpointConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Endpoint))
                throw new InvalidOperationException("Chat client endpoint is not set");
            if (string.IsNullOrWhiteSpace(config.ModelId))
                throw new InvalidOperationException("Chat client model id is not set");
            IChatClient innerChatClient;
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                innerChatClient = new OllamaChatClient(new Uri(config.Endpoint), config.ModelId);
            else
                innerChatClient = new OpenAIChatClient(new(new(config.ApiKey), new()
                {
                    Endpoint = new(config.Endpoint),
                }), config.ModelId);

            var client = new ChatClientBuilder(innerChatClient)
                .UseDistributedCache(_serviceProvider.GetRequiredService<IDistributedCache>())
                //.UseLogging(_serviceProvider.GetRequiredService<ILoggerFactory>())
                .Build();
            return client;
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
        ///     Get first chat client or random chat client based on configuration
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IChatClient GetFirstChatClient()
        {
            if (_chatClients.Count == 0 || !_isEnabled)
                throw new InvalidOperationException("Chat client is not enabled");

            var random = _configuration.GetValue<bool>("AI:FirstServiceRandom");
            return random ? _chatClients[Random.Shared.Next(_chatClients.Count)] : _chatClients[0];
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
        ///     Get config value
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetConfig<T>(string key)
        {
            return _configuration.GetSection($"AI:Config:{key}").Get<T>();
        }

        /// <summary>
        ///     Get assistant
        /// </summary>
        /// <param name="type"></param>
        /// <param name="promptMessage"></param>
        /// <param name="chatClient"></param>
        /// <returns></returns>
        public bool GetAssistant(string type, [NotNullWhen(true)] out ChatMessage? promptMessage,
            [NotNullWhen(true)] out IChatClient? chatClient)
        {
            promptMessage = null;
            chatClient = null;
            var assistantConfig = _configuration.GetSection($"AI:Assistant:{type}").Get<AssistantConfig>();
            if (assistantConfig is null)
            {
                _logger.LogWarning("Assistant {Type} not found", type);
                return false;
            }

            if (!assistantConfig.Enabled)
            {
                _logger.LogWarning("Assistant {Type} is disabled", type);
                return false;
            }

            try
            {
                chatClient = CreateChatClient(assistantConfig.Service);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to create chat client for assistant {Type}", type);
                return false;
            }

            if (GetPrompt(assistantConfig.PromptConfig, out var prompt))
            {
                promptMessage = new(ChatRole.System, prompt);
                return true;
            }

            _logger.LogWarning("Assistant {Type} prompt is empty", type);
            return false;
        }

        /// <summary>
        ///     Check if assistant is enabled
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool CheckAssistantEnabled(string type)
        {
            var assistantConfig = _configuration.GetSection($"AI:Assistant:{type}").Get<AssistantConfig>();
            if (assistantConfig is not null) return assistantConfig.Enabled;
            _logger.LogWarning("Assistant {Type} not found", type);
            return false;
        }

        /// <summary>
        ///     Format assistant message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public string FormatAssistantMessage(string message)
        {
            var format = _configuration.GetValue<string>("AI:AssistantPromptFormat");
            return string.IsNullOrWhiteSpace(format) ? message : string.Format(format, message);
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
                if (!GetPrompt(extension, out var prompt)) continue;
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
            if (!GetPrompt(roleData, out var prompt))
            {
                _logger.LogWarning("Role {Type} prompt is empty", type);
                return false;
            }

            prompt = new StringBuilder(prompt).AppendLine().Append(GetPromptExtensions()).ToString().Trim();
            chatMessage = new(ChatRole.System, prompt);
            return true;
        }

        private static bool GetPrompt(PromptConfig config, [NotNullWhen(true)] out string? prompt)
        {
            prompt = config.Prompt.Trim();
            if (!string.IsNullOrEmpty(prompt)) return true;
            if (string.IsNullOrWhiteSpace(config.PromptFile)) return false;

            if (!File.Exists(config.PromptFile)) return false;

            prompt = File.ReadAllText(config.PromptFile).Trim();
            return !string.IsNullOrEmpty(prompt);
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
        /// <param name="type"></param>
        /// <param name="userId"></param>
        /// <param name="limit"></param>
        /// <param name="excludeUserId"></param>
        /// <returns></returns>
        public async Task<JObject> GetMemory(ChatMemoryType type = ChatMemoryType.ShortTerm, ulong userId = 0,
            int limit = 0, bool excludeUserId = false)
        {
            var table = _databaseProviderService.Table<ChatMemory>();
            ChatMemory[]? memory;
            if (userId == 0)
            {
                if (limit > 0)
                    memory = await table.Where(x => x.Type == type)
                        .OrderByDescending(x => x.Timestamp)
                        .Take(limit)
                        .OrderBy(x => x.Timestamp)
                        .ToArrayAsync().ConfigureAwait(false);
                else
                    memory = await table.Where(x => x.Type == type)
                        .OrderBy(x => x.Timestamp)
                        .ToArrayAsync().ConfigureAwait(false);
            }
            else if (excludeUserId)
            {
                if (limit > 0)
                    memory = await table.Where(x => x.UserId != userId && x.Type == type)
                        .OrderByDescending(x => x.Timestamp)
                        .Take(limit)
                        .OrderBy(x => x.Timestamp)
                        .ToArrayAsync().ConfigureAwait(false);
                else
                    memory = await table.Where(x => x.UserId != userId && x.Type == type)
                        .OrderBy(x => x.Timestamp)
                        .ToArrayAsync().ConfigureAwait(false);
            }
            else if (limit > 0)
            {
                memory = await table.Where(x => x.UserId == userId && x.Type == type)
                    .OrderByDescending(x => x.Timestamp)
                    .Take(limit)
                    .OrderBy(x => x.Timestamp)
                    .ToArrayAsync().ConfigureAwait(false);
            }
            else
            {
                memory = await table.Where(x => x.UserId == userId && x.Type == type)
                    .OrderBy(x => x.Timestamp)
                    .ToArrayAsync().ConfigureAwait(false);
            }

            if (limit > 0 && memory.Length > limit)
                memory = [.. memory.Take(limit)];

            if (memory.Length == 0) return [];

            var userMemory = new Dictionary<ulong, List<JObject>>();
            foreach (var item in memory)
            {
                if (!userMemory.TryGetValue(item.UserId, out var list))
                {
                    list = [];
                    userMemory[item.UserId] = list;
                }

                list.Add(new()
                {
                    ["key"] = item.Key,
                    ["value"] = item.Value,
                });
            }

            return JObject.FromObject(userMemory);
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
        /// <param name="extraData"></param>
        /// <returns></returns>
        public async Task<ChatMessage?> BuildUserChatMessage(string name, ulong? id, DateTimeOffset time,
            string message, JObject? extraData = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (string.IsNullOrWhiteSpace(message)) return null;
            var data = new JObject
            {
                ["time"] = time.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            };

            if (id.HasValue)
            {
                var (_, userInfo) = await _databaseProviderService.GetOrCreateAsync<ChatUserInformation>(id.Value)
                    .ConfigureAwait(false);
                var shortMemory = await GetMemory(userId: id.Value).ConfigureAwait(false);
                var longMemory = await GetMemory(ChatMemoryType.LongTerm).ConfigureAwait(false);
                data["short_memory"] = shortMemory;
                data["long_memory"] = longMemory;
                data["good"] = userInfo.Good;
            }

            if (extraData is not null)
                data["extraData"] = extraData;

            var jObject = new JObject
            {
                ["name"] = name,
                ["discord_id"] = id,
                ["message"] = message,
                ["data"] = data,
            };
            return new(ChatRole.User, jObject.ToString(Formatting.None));
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
            if (response is not ['{', ..] && response is not ['[', ..])
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

            content = jsonStringBuilder.Length < response.Length
                ? response[(jsonStringBuilder.Length + 1)..]
                : string.Empty;
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
                return (false, content, null, null);
            }

            {
                string thinkContent;
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

                if (string.IsNullOrWhiteSpace(content)) return (false, string.Empty, null, sb.ToString());
                var hasJsonHeader = CheckJsonHeader(content, out content, out var jsonHeader);
                return (hasJsonHeader, content, jsonHeader, sb.ToString());
            }
        }

        internal record EndpointConfig(string Endpoint, string ModelId, string ApiKey = "");

        internal record PromptConfig(string Prompt = "", string PromptFile = "");

        internal record PromptExtensionConfig(string Prompt = "", string PromptFile = "")
            : PromptConfig(Prompt, PromptFile);

        internal record RoleConfig(string Prompt = "", string PromptFile = "", float Temperature = 0.6f)
            : PromptConfig(Prompt, PromptFile);

        internal record AssistantConfig(bool Enabled, EndpointConfig Service, PromptConfig PromptConfig);
    }
}