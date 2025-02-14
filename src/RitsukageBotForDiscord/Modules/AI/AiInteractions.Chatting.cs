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
            int retry = 0, float temperature = 1.0f, bool showBtn = true, CancellationToken cancellationToken = default)
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
                if (showBtn)
                    x.Components = new ComponentBuilder()
                        .WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger).Build();
            }).ConfigureAwait(false);

            var endpointConfig = ChatClientProvider.GetFirstChatEndpoint();
            var (isSuccess, errorMessage) =
                await TryGettingResponse(messageList, role, endpointConfig, temperature,
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
                    }).ConfigureAwait(false);
                    (isSuccess, errorMessage) =
                        await TryGettingResponse(messageList, role, endpointConfig,
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
            var lockObject = new Lock();
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
                                           new() { Temperature = temperature, MaxOutputTokens = 8192 },
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
            var embedProcessed = false;
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
                        if (!embedProcessed && resultEmbeds is not null && resultEmbeds.Length > 0)
                        {
                            embedProcessed = true;
                            var recordResultEmbed = resultEmbeds;
                            if (checkedEmbed)
                            {
                                await ModifyOriginalResponseAsync(x =>
                                {
                                    x.Content = updateContent;
                                    x.Embeds = recordResultEmbed.Select(embed => embed.Build()).ToArray();
                                }).ConfigureAwait(false);
                            }
                            else
                            {
                                checkedEmbed = true;
                                await ModifyOriginalResponseAsync(x =>
                                {
                                    x.Content = updateContent;
                                    x.Embeds = recordResultEmbed.Select(embed => embed.Build()).ToArray();
                                }).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            if (checkedEmbed)
                            {
                                await ModifyOriginalResponseAsync(x => { x.Content = updateContent; })
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                checkedEmbed = true;
                                await ModifyOriginalResponseAsync(x =>
                                {
                                    x.Content = updateContent;
                                    x.Embed = null;
                                }).ConfigureAwait(false);
                            }
                        }
                    }

                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (!actionProcessed && hasJsonHeader)
                    resultEmbeds = await ProgressActions(jsonHeader!).ConfigureAwait(false);

                if (!embedProcessed && resultEmbeds is not null && resultEmbeds.Length > 0)
                    await Task.Delay(2000, CancellationToken.None).ContinueWith(async _ =>
                    {
                        await ModifyOriginalResponseAsync(x =>
                        {
                            var list = new List<Embed>();
                            if (x.Embeds.IsSpecified)
                                list.AddRange(x.Embeds.Value);
                            list.AddRange(resultEmbeds.Select(embed => embed.Build()));
                            x.Embeds = list.ToArray();
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
            var embedBuilders = await TryPostprocessMessage(userMessageObject.Message, content,
                JArray.Parse(jsonHeader ?? "[]"),
                cancellationToken).ConfigureAwait(false);
            await InsertChatHistory(Context.Interaction.CreatedAt, userMessageObject.Message, content)
                .ConfigureAwait(false);
            if (embedBuilders.Length > 0)
                await ModifyOriginalResponseAsync(x =>
                {
                    var list = new List<Embed>();
                    if (x.Embeds.IsSpecified)
                        list.AddRange(x.Embeds.Value);
                    list.AddRange(embedBuilders.Select(embed => embed.Build()));
                    x.Embeds = list.ToArray();
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
                                var embed = await ModifyGood(data).ConfigureAwait(false);
                                if (embed is not null && showGoodChange)
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

        private async Task<EmbedBuilder?> ModifyGood(JObject data)
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
        }
    }
}