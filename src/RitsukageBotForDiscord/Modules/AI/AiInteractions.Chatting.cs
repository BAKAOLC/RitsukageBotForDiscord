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
            PreprocessingActionData[]? preprocessingActionData = null,
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
                await TryGettingResponse(messageList, role, preprocessingActionData,
                        endpointConfig, temperature, timeout, cancellationToken)
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
                        await TryGettingResponse(messageList, role, preprocessingActionData, endpointConfig,
                                timeout: timeout, cancellationToken: cancellationToken)
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
            PreprocessingActionData[]? preprocessingActionData = null,
            ChatClientProviderService.EndpointConfig? endpointConfig = null, float temperature = 1.0f,
            long timeout = 60000, CancellationToken cancellationToken = default)
        {
            endpointConfig ??= ChatClientProvider.GetChatEndpointRandomly();
            var chatClient = ChatClientProvider.GetChatClient(endpointConfig);
            var sb = new StringBuilder();
            var haveContent = false;
            var isCompleted = false;
            var isUpdated = false;
            var isError = false;
            var isTimeout = false;
            Exception? exception = null;
            var lockObject = new Lock();
            Embed? preprocessingEmbed = null;
            var showPreprocessingAction = ChatClientProvider.GetConfig<bool>("ShowPreprocessingAction");
            if (preprocessingActionData is not null && showPreprocessingAction)
            {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithDescription(string.Join(Environment.NewLine,
                    preprocessingActionData.Select(x => x.Action)));
                embedBuilder.WithColor(Color.DarkPurple);
                preprocessingEmbed = embedBuilder.Build();
            }

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
                            if (preprocessingEmbed is not null)
                                embeds.Add(preprocessingEmbed);
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
                        {
                            x.Embed = null;
                        }
                        else
                        {
                            var embeds = new List<Embed>();
                            if (preprocessingEmbed is not null)
                                embeds.Add(preprocessingEmbed);
                            embeds.AddRange(resultEmbeds.Select(embed => embed.Build()));
                            x.Embeds = embeds.ToArray();
                        }

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
            {
                var embeds = new List<Embed>();
                if (preprocessingEmbed is not null)
                    embeds.Add(preprocessingEmbed);
                if (resultEmbeds is { Length: > 0 })
                    embeds.AddRange(resultEmbeds.Select(embed => embed.Build()));
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = messageContent;
                    x.Components = null;
                    x.Embeds = embeds.ToArray();
                }).ConfigureAwait(false);
            }

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
                var embeds = new List<Embed>();
                if (preprocessingEmbed is not null)
                    embeds.Add(preprocessingEmbed);
                if (resultEmbeds is not null)
                    embeds.AddRange(resultEmbeds.Select(embed => embed.Build()));
                embeds.Add(assistantEmbed.Build());
                x.Embeds = embeds.ToArray();
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
                var embeds = new List<Embed>();
                if (preprocessingEmbed is not null)
                    embeds.Add(preprocessingEmbed);
                if (resultEmbeds is not null)
                    embeds.AddRange(resultEmbeds.Select(embed => embed.Build()));
                embeds.AddRange(embedBuilders.Select(embed => embed.Build()));
                if (embeds.Count > 0)
                    x.Embeds = embeds.ToArray();
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
        }
    }
}