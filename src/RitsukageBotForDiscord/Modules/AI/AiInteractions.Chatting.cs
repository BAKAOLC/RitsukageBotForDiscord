using System.Text;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task BeginChatAsync(IList<ChatMessage> messageList, string role,
            int retry = 0, float temperature = 1.0f, CancellationToken cancellationToken = default)
        {
            if (!CheckUserInputMessage(messageList))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embed = embed.Build();
                }).ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("User {UserId} sent a message to chat with AI: {Message}", Context.User.Id,
                FormatJson(messageList.Last(x => x.Role == ChatRole.User).ToString()));

            var waitEmbed = new EmbedBuilder();
            waitEmbed.WithTitle("Chatting with AI");
            waitEmbed.WithDescription("Getting response from the AI...");
            waitEmbed.WithColor(Color.Orange);

            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embed = waitEmbed.Build();
                x.Components = new ComponentBuilder()
                    .WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger).Build();
            }).ConfigureAwait(false);

            var endpointConfig = ChatClientProvider.GetFirstChatEndpoint();
            var timeout = ChatClientProvider.GetConfig<long?>("Timeout") ?? 60000;
            var (isSuccess, errorMessage) =
                await TryGettingResponse(messageList, role, endpointConfig, temperature, timeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            if (isSuccess) return;
            if (cancellationToken.IsCancellationRequested) return;

            if (retry > 0 && !cancellationToken.IsCancellationRequested)
            {
                var clients = ChatClientProvider.GetEndpointConfigs();
                for (var i = 0; i < retry; i++)
                {
                    if (clients.Length > 1)
                    {
                        var currentEndpoint = endpointConfig;
                        var otherClients = clients.Where(x => x != currentEndpoint).ToArray();
                        if (otherClients.Length > 0)
                            endpointConfig = otherClients[Random.Shared.Next(otherClients.Length)];
                    }


                    var retryMessage = $"{errorMessage}\nRetrying... ({i + 1}/{retry})";
                    var retryEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = retryMessage,
                        Color = Color.Red,
                    };
                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = retryEmbed.Build();
                        x.Components = new ComponentBuilder()
                            .WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger).Build();
                    }).ConfigureAwait(false);
                    (isSuccess, errorMessage) =
                        await TryGettingResponse(messageList, role, endpointConfig, timeout: timeout,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    if (isSuccess) return;
                    if (cancellationToken.IsCancellationRequested) return;
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            var errorEmbed = new EmbedBuilder
            {
                Title = "Error",
                Description = errorMessage,
                Color = Color.Red,
            };
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embed = errorEmbed.Build();
                x.Components = null;
            }).ConfigureAwait(false);
        }

        // ReSharper disable once CyclomaticComplexity
        private async Task<(bool, string?)> TryGettingResponse(IList<ChatMessage> messageList, string role,
            ChatClientProviderService.EndpointConfig? endpointConfig = null, float temperature = 1.0f,
            long timeout = 60000, CancellationToken cancellationToken = default)
        {
            endpointConfig ??= ChatClientProvider.GetChatEndpointRandomly();
            var chatClient = ChatClientProvider.GetChatClient(endpointConfig);
            var sb = new StringBuilder();
            var haveContent = false;
            var checkedEmbed = false;
            var isCompleted = false;
            var isUpdated = false;
            var isError = false;
            var isTimeout = false;
            Exception? exception = null;
            var lockObject = new object();
            var generatingEmbed = new EmbedBuilder();
            generatingEmbed.WithDescription("Generating the response...");
            generatingEmbed.WithColor(Color.Orange);
            {
                var cancellationTokenSource1 = new CancellationTokenSource();
                var cancellationTokenSource2 = new CancellationTokenSource();
                _ = Task.Delay(TimeSpan.FromMilliseconds(timeout), cancellationTokenSource1.Token)
                    .ContinueWith(x =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (x.IsFaulted) return;
                        lock (lockObject)
                        {
                            if (haveContent) return;
                            cancellationTokenSource2.Cancel();
                            isTimeout = true;
                            Logger.LogWarning(
                                "It took too long to get a response from {ModelId} in {Url} with role: {Role}",
                                endpointConfig.ModelId, endpointConfig.Endpoint, role);
                        }
                    }, cancellationTokenSource1.Token);
                _ = Task.Run(async () =>
                    {
                        await foreach (var response in chatClient.CompleteStreamingAsync(messageList,
                                           new()
                                           {
                                               Temperature = temperature,
                                               MaxOutputTokens = 8192,
                                               // Properties for Grok
                                               AdditionalProperties = new(new Dictionary<string, object?>
                                               {
                                                   { "max_completion_tokens", 8192 },
                                                   { "search_parameters", new Dictionary<string, object>() },
                                               }),
                                           },
                                           cancellationTokenSource2.Token))
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            lock (lockObject)
                            {
                                if (string.IsNullOrWhiteSpace(response.ToString()))
                                    continue;
                                sb.Append(response);
                                isUpdated = true;
                                haveContent = true;
                            }
                        }

                        isCompleted = true;
                        await cancellationTokenSource1.CancelAsync().ConfigureAwait(false);
                    }, cancellationTokenSource2.Token)
                    .ContinueWith(x =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (!x.IsFaulted) return;
                        if (isTimeout) return;
                        isError = true;
                        exception = x.Exception;
                        cancellationTokenSource1.Cancel();
                        Logger.LogError(x.Exception,
                            "An error occurred while getting a response from {ModelId} in {Url} with role: {Role}",
                            endpointConfig.ModelId, endpointConfig.Endpoint, role);
                    }, cancellationTokenSource2.Token);
            }

            var actionProcessed = false;
            var hasJsonHeader = false;
            var content = string.Empty;
            string? jsonHeader = null;
            EmbedBuilder[]? resultEmbeds = null;
            try
            {
                while (!isCompleted && !isError && !isTimeout && !cancellationToken.IsCancellationRequested)
                {
                    lock (lockObject)
                    {
                        if (isUpdated)
                        {
                            (hasJsonHeader, content, jsonHeader, _) =
                                ChatClientProviderService.FormatResponse(sb.ToString());
                            isUpdated = false;
                        }
                    }

                    if (!actionProcessed && hasJsonHeader)
                    {
                        resultEmbeds = await ProgressActions(jsonHeader!).ConfigureAwait(false);
                        actionProcessed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var updateContent =
                            $"|| Generated by {endpointConfig.GetName()} with role: {role} ||\n{content}";
                        var recordResultEmbed = resultEmbeds;
                        await ModifyOriginalResponseAsync(x =>
                        {
                            x.Content = updateContent;
                            var embeds = new List<Embed>();
                            if (recordResultEmbed is not null)
                                embeds.AddRange(recordResultEmbed.Select(embed => embed.Build()));
                            embeds.Add(generatingEmbed.Build());
                            x.Embeds = embeds.ToArray();
                            x.Components = null;
                        }).ConfigureAwait(false);
                    }

                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (!actionProcessed && hasJsonHeader)
                    resultEmbeds = await ProgressActions(jsonHeader!).ConfigureAwait(false);

                await Task.Delay(2000, CancellationToken.None).ContinueWith(async _ =>
                {
                    await ModifyOriginalResponseAsync(x =>
                    {
                        if (resultEmbeds is null || resultEmbeds.Length == 0)
                            x.Embed = null;
                        else
                            x.Embeds = resultEmbeds.Select(embed => embed.Build()).ToArray();
                        x.Components = null;
                    }).ConfigureAwait(false);
                }, CancellationToken.None).ConfigureAwait(false);
                return (false, "The chat with AI was canceled");
            }
            catch (Exception ex)
            {
                isError = true;
                exception = ex;
                Logger.LogError(ex,
                    "An error occurred while getting a response from {ModelId} in {Url} with role: {Role}",
                    endpointConfig.ModelId, endpointConfig.Endpoint, role);
            }

            if (isError)
            {
                if (exception is not null)
                    return (false,
                        $"An error occurred while getting a response from {endpointConfig.GetName()} with role: {role}\n{exception.Message})");
                return (false,
                    $"An error occurred while getting a response from {endpointConfig.GetName()} with role: {role}");
            }

            if (isTimeout)
                return (false,
                    $"It took too long to get a response from {endpointConfig.GetName()} with role: {role}");

            (hasJsonHeader, content, jsonHeader, var thinkContent) =
                ChatClientProviderService.FormatResponse(sb.ToString());
            if (!string.IsNullOrWhiteSpace(thinkContent))
                Logger.LogInformation("Think content for with {ModelId} from {Url} in role: {Role}:\n{ThinkContent}",
                    endpointConfig.ModelId, endpointConfig.Endpoint, role, thinkContent);

            if (!actionProcessed && hasJsonHeader)
                resultEmbeds = await ProgressActions(jsonHeader!).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested) return (false, "The chat with AI was canceled");

            if (string.IsNullOrWhiteSpace(content)) return (true, null);
            var messageContent = $"|| Generated by {endpointConfig.GetName()} with role: {role} ||\n{content}";
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = messageContent;
                x.Components = null;
                x.Embeds = resultEmbeds?.Select(embed => embed.Build()).ToArray();
            }).ConfigureAwait(false);

            var userMessage = messageList.Last(x => x.Role == ChatRole.User).ToString();
            var userMessageData = JObject.Parse(userMessage);
            var userMessageObject = userMessageData.ToObject<UserMessage>();
            if (userMessageObject is null) return (true, null);
            if (!ChatClientProvider.CheckAssistantEnabled("Postprocessing"))
            {
                await InsertChatHistory(Context.Interaction.CreatedAt, userMessageObject.Message, content)
                    .ConfigureAwait(false);
                return (true, null);
            }

            var assistantEmbed = new EmbedBuilder();
            assistantEmbed.WithDescription("Postprocessing the response...");
            assistantEmbed.WithColor(Color.Orange);
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = messageContent;
                x.Components = null;
                var list = new List<Embed>();
                if (resultEmbeds is not null)
                    list.AddRange(resultEmbeds.Select(embed => embed.Build()));
                list.Add(assistantEmbed.Build());
                x.Embeds = list.ToArray();
            }).ConfigureAwait(false);
            var embedBuilders = await TryPostprocessingMessage(userMessageObject.Message, content,
                JArray.Parse(jsonHeader ?? "[]"),
                cancellationToken).ConfigureAwait(false);
            await InsertChatHistory(Context.Interaction.CreatedAt, userMessageObject.Message, content)
                .ConfigureAwait(false);
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = messageContent;
                x.Components = null;
                var list = new List<Embed>();
                if (resultEmbeds is not null)
                    list.AddRange(resultEmbeds.Select(embed => embed.Build()));
                list.AddRange(embedBuilders.Select(embed => embed.Build()));
                if (list.Count > 0)
                    x.Embeds = list.ToArray();
                else
                    x.Embed = null;
            }).ConfigureAwait(false);
            return (true, null);
        }

        private async Task InsertChatHistory(DateTimeOffset time, string message, string reply)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(reply)) return;
            var key = $"chat_history_{time.ConvertToSettingsOffset().ToDateTimeStringWithoutSpace()}";
            var value = new JObject
            {
                ["message"] = message,
                ["reply"] = reply,
            }.ToString(Formatting.None);
            await ChatClientProvider.InsertMemory(Context.User.Id, ChatMemoryType.LongTerm, key, value)
                .ConfigureAwait(false);
        }

        // ReSharper disable once CyclomaticComplexity
        private async Task<EmbedBuilder[]> ProgressActions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            var result = new List<EmbedBuilder>();
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Processing the JSON header: {ActionArrayData}", json);
                var showGoodChange = ChatClientProvider.GetConfig<bool>("ShowGoodChange");
                var showMemoryChange = ChatClientProvider.GetConfig<bool>("ShowMemoryChange");
                foreach (var data in actionArrayData.OfType<JObject>())
                    try
                    {
                        var actionType = data.Value<string>("action");
                        if (string.IsNullOrWhiteSpace(actionType))
                        {
                            Logger.LogWarning("Unable to parse the JSON: {Json}", data.ToString());
                            continue;
                        }

                        switch (actionType)
                        {
                            case "good":
                            {
                                var embed = await ProcessingModifyGood(data).ConfigureAwait(false);
                                if (embed is not null && showGoodChange)
                                    result.Add(embed);
                                break;
                            }
                            case "add_short_memory":
                            {
                                var embed = await ProcessingAddShortMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "add_long_memory":
                            {
                                var embed = await ProcessingAddLongMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "update_self_state":
                            {
                                var embed = await ProcessingUpdateSelfState(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "remove_long_memory":
                            case "remove_chat_history":
                            {
                                var embed = await ProcessingRemoveLongMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "remove_self_state":
                            {
                                var embed = await ProcessingRemoveSelfState(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "query_user_id":
                            {
                                var embed = await ProcessingQueryUserId(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "get_user_info":
                            {
                                var embed = await ProcessingGetUserInfo(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "list_channels":
                            {
                                var embed = await ProcessingListChannels(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "send_dm":
                            {
                                var embed = await ProcessingSendDm(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "send_channel_message":
                            {
                                var embed = await ProcessingSendChannelMessage(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "random_users":
                            {
                                var embed = await ProcessingRandomUsers(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "create_scheduled_task":
                            {
                                var embed = await ProcessingCreateScheduledTask(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "list_scheduled_tasks":
                            {
                                var embed = await ProcessingListScheduledTasks(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "delete_scheduled_task":
                            {
                                var embed = await ProcessingDeleteScheduledTask(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "toggle_scheduled_task":
                            {
                                var embed = await ProcessingToggleScheduledTask(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while processing the JSON action: {Json}", data.ToString());
                    }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while parsing the JSON header: {JsonHeader}", json);
                var errorEmbed = new EmbedBuilder();
                errorEmbed.WithColor(Color.Red);
                errorEmbed.WithDescription("An error occurred while processing the response");
                return [errorEmbed];
            }

            return [.. result];
        }

        private async Task<EmbedBuilder?> ProcessingModifyGood(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for good action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for good action");
            var param = paramToken.ToObject<ActionParam.GoodActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for good action");
            if (param.Value == 0) return null;

            var (_, userRecord) = await DatabaseProviderService
                .GetOrCreateAsync<ChatUserInformation>(Context.User.Id)
                .ConfigureAwait(false);
            userRecord.Good += param.Value;
            await DatabaseProviderService.InsertOrUpdateAsync(userRecord).ConfigureAwait(false);
            await ChatClientProvider.RecordChatDataChangeHistory(Context.User.Id, "good", param.Value, param.Reason,
                Context.Interaction.CreatedAt).ConfigureAwait(false);
            Color color;
            string modifyTag;
            if (param.Value > 0)
            {
                color = Color.Green;
                modifyTag = "Increased";
            }
            else
            {
                color = Color.Red;
                modifyTag = "Decreased";
            }

            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(color);
            embedBuilder.WithDescription(
                string.IsNullOrWhiteSpace(param.Reason)
                    ? $"{modifyTag} by {Math.Abs(param.Value)} points, current points: {userRecord.Good}"
                    : $"{modifyTag} by {Math.Abs(param.Value)} points, current points: {userRecord.Good} ({param.Reason})");
            return embedBuilder;
        }

        private async Task<EmbedBuilder?> ProcessingAddShortMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            var param = paramToken.ToObject<ActionParam.MemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");

            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.ShortTerm, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            if (sb.Length == 0) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added short-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingAddLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            var param = paramToken.ToObject<ActionParam.MemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");

            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.LongTerm, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            if (sb.Length == 0) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added long-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingRemoveLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            var param = paramToken.ToObject<ActionParam.RemoveMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (param.Keys.Length == 0)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");

            var sb = new StringBuilder();
            foreach (var key in param.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, key)
                    .ConfigureAwait(false);
                sb.Append($"{key}\n");
            }

            if (sb.Length == 0) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed long-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingUpdateSelfState(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for update_self_state action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for update_self_state action");
            var param = paramToken.ToObject<ActionParam.MemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for update_self_state action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for update_self_state action");
            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.SelfState, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            if (sb.Length == 0) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Updated self state: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingRemoveSelfState(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            var param = paramToken.ToObject<ActionParam.RemoveMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            if (param.Keys.Length == 0)
                throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            var sb = new StringBuilder();
            foreach (var key in param.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.SelfState, key)
                    .ConfigureAwait(false);
                sb.Append($"{key}\n");
            }

            if (sb.Length == 0) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed self state: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingQueryUserId(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for query_user_id action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for query_user_id action");
            var param = paramToken.ToObject<ActionParam.QueryUserIdActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for query_user_id action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var users = Context.Guild.Users
                .Where(u => u.Username.Contains(param.Username, StringComparison.OrdinalIgnoreCase) ||
                           (u.GlobalName?.Contains(param.Username, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(10)
                .ToList();

            if (!users.Any())
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "User Search",
                    Description = $"No users found with username containing: {param.Username}",
                    Color = Color.Orange,
                };
                return notFoundEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "User Search Results",
                Description = $"Found {users.Count} user(s) matching: {param.Username}",
                Color = Color.Green,
            };

            foreach (var user in users)
            {
                var fieldName = user.GlobalName ?? user.Username;
                var fieldValue = $"ID: `{user.Id}`\nUsername: {user.Username}\nMention: <@{user.Id}>";
                embed.AddField(fieldName, fieldValue, true);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingGetUserInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for get_user_info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for get_user_info action");
            var param = paramToken.ToObject<ActionParam.GetUserInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for get_user_info action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            if (!ulong.TryParse(param.UserId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var user = Context.Guild.GetUser(id);
            if (user is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "User Information",
                    Description = $"User with ID `{id}` not found in this server.",
                    Color = Color.Orange,
                };
                return notFoundEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "User Information",
                Color = Color.Blue,
            };

            embed.WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());
            embed.AddField("Display Name", user.DisplayName, true);
            embed.AddField("Username", user.Username, true);
            embed.AddField("User ID", user.Id.ToString(), true);
            embed.AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F>", true);
            embed.AddField("Joined Server", user.JoinedAt.HasValue ? $"<t:{user.JoinedAt.Value.ToUnixTimeSeconds()}:F>" : "Unknown", true);
            embed.AddField("Is Bot", user.IsBot ? "Yes" : "No", true);

            if (user.Roles.Any(r => r.Id != Context.Guild.EveryoneRole.Id))
            {
                var roles = string.Join(", ", user.Roles.Where(r => r.Id != Context.Guild.EveryoneRole.Id).Select(r => r.Mention));
                embed.AddField("Roles", roles.Length > 1024 ? "Too many roles to display" : roles);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingListChannels(JObject data)
        {
            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var textChannels = Context.Guild.TextChannels
                .Where(c => Context.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                .OrderBy(c => c.Position)
                .ToList();

            var voiceChannels = Context.Guild.VoiceChannels
                .Where(c => Context.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                .OrderBy(c => c.Position)
                .ToList();

            var embed = new EmbedBuilder
            {
                Title = "Accessible Channels",
                Color = Color.Green,
            };

            if (textChannels.Any())
            {
                var textChannelList = string.Join("\n", textChannels.Take(20).Select(c => $"<#{c.Id}> (`{c.Id}`)"));
                if (textChannels.Count > 20)
                    textChannelList += $"\n... and {textChannels.Count - 20} more";
                embed.AddField($"Text Channels ({textChannels.Count})", textChannelList);
            }

            if (voiceChannels.Any())
            {
                var voiceChannelList = string.Join("\n", voiceChannels.Take(20).Select(c => $"{c.Name} (`{c.Id}`)"));
                if (voiceChannels.Count > 20)
                    voiceChannelList += $"\n... and {voiceChannels.Count - 20} more";
                embed.AddField($"Voice Channels ({voiceChannels.Count})", voiceChannelList);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingSendDm(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for send_dm action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for send_dm action");
            var param = paramToken.ToObject<ActionParam.SendDmActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for send_dm action");

            // Check if the bot owner is performing this action (security check)
            if (Context.User.Id != Context.Client.Application.Owner.Id)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "Only the bot owner can send direct messages through AI actions.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            if (!ulong.TryParse(param.UserId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var user = Context.Client.GetUser(id);
            if (user is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"User with ID `{id}` not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            try
            {
                await user.SendMessageAsync(param.Message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent private message to {user.Username} (`{user.Id}`)",
                    Color = Color.Green,
                };
                return successEmbed;
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }
        }

        private async Task<EmbedBuilder?> ProcessingSendChannelMessage(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for send_channel_message action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for send_channel_message action");
            var param = paramToken.ToObject<ActionParam.SendChannelMessageActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for send_channel_message action");

            // Check if the user has admin permissions (security check)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "Only administrators can send channel messages through AI actions.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            if (!ulong.TryParse(param.ChannelId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid channel ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var channel = Context.Client.GetChannel(id) as IMessageChannel;
            if (channel is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Channel with ID `{id}` not found or is not a message channel.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            try
            {
                await channel.SendMessageAsync(param.Message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent message to <#{channel.Id}>",
                    Color = Color.Green,
                };
                return successEmbed;
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }
        }

        private async Task<EmbedBuilder?> ProcessingRandomUsers(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for random_users action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for random_users action");
            var param = paramToken.ToObject<ActionParam.RandomUsersActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for random_users action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var count = Math.Clamp(param.Count, 1, 10);
            var users = Context.Guild.Users
                .Where(u => !u.IsBot && u.Status != UserStatus.Offline)
                .ToList();

            if (!users.Any())
            {
                var noUsersEmbed = new EmbedBuilder
                {
                    Title = "Random Users",
                    Description = "No online users found in this server.",
                    Color = Color.Orange,
                };
                return noUsersEmbed;
            }

            var randomUsers = users.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();

            var embed = new EmbedBuilder
            {
                Title = "Random Users",
                Description = $"Selected {randomUsers.Count} random online user(s):",
                Color = Color.Blue,
            };

            foreach (var user in randomUsers)
            {
                var fieldName = user.DisplayName;
                var fieldValue = $"Username: {user.Username}\nID: `{user.Id}`\nStatus: {user.Status}";
                embed.AddField(fieldName, fieldValue, true);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingCreateScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");
            var param = paramToken.ToObject<ActionParam.CreateScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Parse schedule time
            if (!DateTime.TryParseExact(param.ScheduleTime, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedTime))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid schedule time format. Use: yyyy-MM-dd HH:mm (e.g., 2024-12-25 14:30)",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var scheduleTimeOffset = new DateTimeOffset(parsedTime, TimeZoneInfo.Local.GetUtcOffset(parsedTime));

            // Validate channel ID if provided
            ulong? channelIdParsed = null;
            if (!string.IsNullOrWhiteSpace(param.ChannelId))
            {
                if (!ulong.TryParse(param.ChannelId, out var cId))
                {
                    var errorEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "Invalid channel ID format.",
                        Color = Color.Red,
                    };
                    return errorEmbed;
                }
                channelIdParsed = cId;

                var channel = Context.Client.GetChannel(cId);
                if (channel is null)
                {
                    var errorEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "Channel not found.",
                        Color = Color.Red,
                    };
                    return errorEmbed;
                }
            }

            // Validate AI role if provided
            if (!string.IsNullOrWhiteSpace(param.AiRole) && !ChatClientProvider.GetRoleData(out _, out _, param.AiRole))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Invalid AI role: {param.AiRole}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Create task
            var task = new AiScheduledTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = param.Name,
                UserId = Context.User.Id,
                GuildId = Context.Guild.Id,
                ChannelId = channelIdParsed,
                Prompt = param.Prompt,
                AiRole = param.AiRole,
                ScheduleType = param.ScheduleType,
                ScheduleTime = scheduleTimeOffset,
                IntervalSeconds = param.ScheduleType is "Periodic" or "Countdown" or "UntilTime" ? param.IntervalMinutes * 60L : null,
                TargetTimes = param.ScheduleType == "Countdown" ? (ulong)param.TargetTimes : null,
                TargetTime = param.ScheduleType == "UntilTime" ? scheduleTimeOffset.AddMinutes(param.IntervalMinutes * param.TargetTimes) : null,
                IsEnabled = true,
                CreatedTime = DateTimeOffset.UtcNow,
                UpdatedTime = DateTimeOffset.UtcNow
            };

            await DatabaseProviderService.InsertOrUpdateAsync(task).ConfigureAwait(false);

            var embed = new EmbedBuilder
            {
                Title = "AI Scheduled Task Created",
                Description = $"Task **{param.Name}** has been created successfully.",
                Color = Color.Green,
            };

            embed.AddField("Task ID", task.Id, true);
            embed.AddField("Schedule Type", param.ScheduleType, true);
            embed.AddField("Schedule Time", $"<t:{scheduleTimeOffset.ToUnixTimeSeconds()}:F>", true);
            embed.AddField("Output", channelIdParsed.HasValue ? $"<#{channelIdParsed.Value}>" : "Direct Message", true);
            if (!string.IsNullOrWhiteSpace(param.AiRole))
                embed.AddField("AI Role", param.AiRole, true);

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingListScheduledTasks(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");
            var param = paramToken.ToObject<ActionParam.ListScheduledTasksActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var query = param.ShowAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? "SELECT * FROM AiScheduledTask WHERE guild_id = ? ORDER BY created_time DESC"
                : "SELECT * FROM AiScheduledTask WHERE guild_id = ? AND user_id = ? ORDER BY created_time DESC";

            var parameters = param.ShowAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? new object[] { Context.Guild.Id }
                : new object[] { Context.Guild.Id, Context.User.Id };

            var tasks = await DatabaseProviderService.QueryAsync<AiScheduledTask>(query, parameters).ConfigureAwait(false);

            if (!tasks.Any())
            {
                var noTasksEmbed = new EmbedBuilder
                {
                    Title = "AI Scheduled Tasks",
                    Description = param.ShowAll ? "No scheduled tasks found in this server." : "You have no scheduled tasks.",
                    Color = Color.Orange,
                };
                return noTasksEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "AI Scheduled Tasks",
                Description = $"Found {tasks.Count} task(s)",
                Color = Color.Blue,
            };

            foreach (var task in tasks.Take(10)) // Limit to first 10 tasks
            {
                var statusIcon = task.IsFinished ? "✅" : (task.IsEnabled ? "🟢" : "⏸️");
                var fieldName = $"{statusIcon} {task.Name}";
                
                var fieldValue = $"ID: `{task.Id[..8]}...`\n" +
                                $"Type: {task.ScheduleType}\n" +
                                $"Schedule: <t:{task.ScheduleTime.ToUnixTimeSeconds()}:R>\n" +
                                $"Executed: {task.ExecutedTimes} times";

                if (task.LastExecutedTime.HasValue)
                    fieldValue += $"\nLast: <t:{task.LastExecutedTime.Value.ToUnixTimeSeconds()}:R>";

                if (param.ShowAll)
                    fieldValue += $"\nCreator: <@{task.UserId}>";

                embed.AddField(fieldName, fieldValue, true);
            }

            if (tasks.Count > 10)
                embed.WithFooter($"Showing 10 of {tasks.Count} tasks");

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessingDeleteScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");
            var param = paramToken.ToObject<ActionParam.DeleteScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            // Check permissions (user can delete own tasks, admins can delete any)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var isTaskCreator = task.UserId == Context.User.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !isTaskCreator && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "You don't have permission to delete this task.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            // Delete the task
            await DatabaseProviderService.DeleteAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);

            var successEmbed = new EmbedBuilder
            {
                Title = "Task Deleted",
                Description = $"Successfully deleted task **{task.Name}** (`{task.Id[..8]}...`)",
                Color = Color.Green,
            };

            successEmbed.WithCurrentTimestamp();
            return successEmbed;
        }

        private async Task<EmbedBuilder?> ProcessingToggleScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");
            var param = paramToken.ToObject<ActionParam.ToggleScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            // Check permissions (same as delete)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var isTaskCreator = task.UserId == Context.User.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !isTaskCreator && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "You don't have permission to modify this task.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            // Toggle the status
            task.IsEnabled = !task.IsEnabled;
            task.UpdatedTime = DateTimeOffset.UtcNow;
            await DatabaseProviderService.InsertOrUpdateAsync(task).ConfigureAwait(false);

            var statusText = task.IsEnabled ? "enabled" : "disabled";
            var statusColor = task.IsEnabled ? Color.Green : Color.Orange;

            var successEmbed = new EmbedBuilder
            {
                Title = "Task Status Updated",
                Description = $"Task **{task.Name}** has been {statusText}.",
                Color = statusColor,
            };

            successEmbed.WithCurrentTimestamp();
            return successEmbed;
        }

        private static bool CheckUserInputMessage(IList<ChatMessage> messageList)
        {
            string? userInputMessage = null;
            var lastUserMessage = messageList.LastOrDefault(x => x.Role == ChatRole.User);
            if (lastUserMessage is not null)
                userInputMessage = lastUserMessage.ToString();
            return !string.IsNullOrWhiteSpace(userInputMessage);
        }

        private class UserMessage
        {
            [JsonProperty("name")] public string Name { get; set; } = string.Empty;
            [JsonProperty("message")] public string Message { get; set; } = string.Empty;
        }

        private static class ActionParam
        {
            internal class GoodActionParam
            {
                [JsonProperty("value")] public int Value { get; set; }

                [JsonProperty("reason")] public string Reason { get; set; } = string.Empty;
            }

            internal class MemoryActionParam
            {
                [JsonProperty("data")] public JObject Data { get; set; } = [];
            }

            internal class RemoveMemoryActionParam
            {
                [JsonProperty("keys")] public string[] Keys { get; set; } = [];
            }

            internal class QueryUserIdActionParam
            {
                [JsonProperty("username")] public string Username { get; set; } = string.Empty;
            }

            internal class GetUserInfoActionParam
            {
                [JsonProperty("userId")] public string UserId { get; set; } = string.Empty;
            }

            internal class SendDmActionParam
            {
                [JsonProperty("userId")] public string UserId { get; set; } = string.Empty;
                [JsonProperty("message")] public string Message { get; set; } = string.Empty;
            }

            internal class SendChannelMessageActionParam
            {
                [JsonProperty("channelId")] public string ChannelId { get; set; } = string.Empty;
                [JsonProperty("message")] public string Message { get; set; } = string.Empty;
            }

            internal class RandomUsersActionParam
            {
                [JsonProperty("count")] public int Count { get; set; } = 5;
            }

            internal class CreateScheduledTaskActionParam
            {
                [JsonProperty("name")] public string Name { get; set; } = string.Empty;
                [JsonProperty("prompt")] public string Prompt { get; set; } = string.Empty;
                [JsonProperty("scheduleTime")] public string ScheduleTime { get; set; } = string.Empty;
                [JsonProperty("scheduleType")] public string ScheduleType { get; set; } = "OneTime";
                [JsonProperty("channelId")] public string? ChannelId { get; set; }
                [JsonProperty("intervalMinutes")] public int IntervalMinutes { get; set; } = 60;
                [JsonProperty("targetTimes")] public int TargetTimes { get; set; } = 1;
                [JsonProperty("aiRole")] public string? AiRole { get; set; }
            }

            internal class ListScheduledTasksActionParam
            {
                [JsonProperty("showAll")] public bool ShowAll { get; set; } = false;
            }

            internal class DeleteScheduledTaskActionParam
            {
                [JsonProperty("taskId")] public string TaskId { get; set; } = string.Empty;
            }

            internal class ToggleScheduledTaskActionParam
            {
                [JsonProperty("taskId")] public string TaskId { get; set; } = string.Empty;
            }
        }
    }
}