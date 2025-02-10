using System.Text;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using IChatClient = Microsoft.Extensions.AI.IChatClient;

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

            var component = new ComponentBuilder();
            component.WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger);

            var waitEmbed = new EmbedBuilder();
            waitEmbed.WithTitle("Chatting with AI");
            waitEmbed.WithDescription("Getting response from the AI...");
            waitEmbed.WithColor(Color.Orange);

            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embed = waitEmbed.Build();
                x.Components = component.Build();
            }).ConfigureAwait(false);

            var client = ChatClientProvider.GetFirstChatClient();
            var (isSuccess, errorMessage) =
                await TryGettingResponse(messageList, role, client, temperature, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            if (isSuccess) return;
            if (cancellationToken.IsCancellationRequested) return;

            if (retry > 0 && !cancellationToken.IsCancellationRequested)
            {
                var clients = ChatClientProvider.GetChatClients();
                for (var i = 0; i < retry; i++)
                {
                    if (clients.Length > 1)
                    {
                        var currentClient = client;
                        var otherClients = clients.Where(x => x != currentClient).ToArray();
                        if (otherClients.Length > 0)
                            client = otherClients[Random.Shared.Next(otherClients.Length)];
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
                        await TryGettingResponse(messageList, role, client, cancellationToken: cancellationToken)
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
            IChatClient? client = null, float temperature = 1.0f,
            long timeout = 60000, CancellationToken cancellationToken = default)
        {
            client ??= ChatClientProvider.GetFirstChatClient();
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
                                client.Metadata.ModelId, client.Metadata.ProviderUri, role);
                        }
                    }, cancellationTokenSource1.Token);
                _ = Task.Run(async () =>
                    {
                        await foreach (var response in client.CompleteStreamingAsync(messageList,
                                           new() { Temperature = temperature, MaxOutputTokens = 8192}, cancellationTokenSource2.Token))
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
                            client.Metadata.ModelId, client.Metadata.ProviderUri, role);
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
                        content =
                            $"|| Generated by {client.Metadata.ModelId} with role: {role} ||\n{content}";
                        var updateContent = content;
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
                    client.Metadata.ModelId, client.Metadata.ProviderUri, role);
            }

            if (isError)
            {
                if (exception is not null)
                    return (false,
                        $"An error occurred while getting a response from {client.Metadata.ModelId} in {client.Metadata.ProviderUri} with role: {role}\n{exception.Message})");
                return (false,
                    $"An error occurred while getting a response from {client.Metadata.ModelId} in {client.Metadata.ProviderUri} with role: {role}");
            }

            if (isTimeout)
                return (false,
                    $"It took too long to get a response from {client.Metadata.ModelId} in {client.Metadata.ProviderUri} with role: {role}");

            (hasJsonHeader, content, jsonHeader, var thinkContent) =
                ChatClientProviderService.FormatResponse(sb.ToString());
            if (!string.IsNullOrWhiteSpace(thinkContent))
                Logger.LogInformation("Think content for with {ModelId} from {Url} in role: {Role}:\n{ThinkContent}",
                    client.Metadata.ModelId, client.Metadata.ProviderUri, role, thinkContent);

            if (!actionProcessed && hasJsonHeader)
                resultEmbeds = await ProgressActions(jsonHeader!).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested) return (false, "The chat with AI was canceled");

            if (string.IsNullOrWhiteSpace(content)) return (true, null);
            content = $"|| Generated by {client.Metadata.ModelId} with role: {role} ||\n{content}";
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = content;
                x.Components = null;
                x.Embeds = resultEmbeds?.Select(embed => embed.Build()).ToArray();
            }).ConfigureAwait(false);

            return (true, null);
        }

        // ReSharper disable once CyclomaticComplexity
        private async Task<EmbedBuilder[]> ProgressActions(string jsonHeader)
        {
            if (string.IsNullOrWhiteSpace(jsonHeader)) return [];
            var result = new List<EmbedBuilder>();
            try
            {
                var actionArrayData = JArray.Parse(jsonHeader);
                Logger.LogInformation("Processing the JSON header: {JsonHeader}", jsonHeader);
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
                                var embed = await ModifyGood(data).ConfigureAwait(false);
                                if (embed is not null)
                                    result.Add(embed);
                                break;
                            }
                            case "add_short_memory":
                            {
                                var embed = await AddShortMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "add_long_memory":
                            {
                                var embed = await AddLongMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "remove_long_memory":
                            {
                                var embed = await RemoveLongMemory(data).ConfigureAwait(false);
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
                Logger.LogError(ex, "Error while parsing the JSON header: {JsonHeader}", jsonHeader);
                var errorEmbed = new EmbedBuilder();
                return [errorEmbed];
            }

            await ChatClientProvider.RefreshShortMemory(Context.User.Id).ConfigureAwait(false);
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

        private async Task<EmbedBuilder?> AddShortMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            var param = paramToken.ToObject<ActionParam.ShortMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (string.IsNullOrWhiteSpace(param.Key) || string.IsNullOrWhiteSpace(param.Value))
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            await ChatClientProvider
                .InsertMemory(Context.User.Id, ChatMemoryType.ShortTerm, param.Key, param.Value)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added short-term memory: {param.Key} = {param.Value}");
            return embed;
        }

        private async Task<EmbedBuilder?> AddLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            var param = paramToken.ToObject<ActionParam.LongMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (string.IsNullOrWhiteSpace(param.Key) || string.IsNullOrWhiteSpace(param.Value))
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            await ChatClientProvider
                .InsertMemory(Context.User.Id, ChatMemoryType.LongTerm, param.Key, param.Value)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added long-term memory: {param.Key} = {param.Value}");
            return embed;
        }

        private async Task<EmbedBuilder?> RemoveLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            var param = paramToken.ToObject<ActionParam.RemoveLongMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (string.IsNullOrWhiteSpace(param.Key))
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, param.Key)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed long-term memory: {param.Key}");
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

        private static class ActionParam
        {
            internal class GoodActionParam
            {
                [JsonProperty("value")] public int Value { get; set; }

                [JsonProperty("reason")] public string Reason { get; set; } = string.Empty;
            }

            internal class ShortMemoryActionParam
            {
                [JsonProperty("key")] public string Key { get; set; } = string.Empty;

                [JsonProperty("value")] public string Value { get; set; } = string.Empty;
            }

            internal class LongMemoryActionParam
            {
                [JsonProperty("key")] public string Key { get; set; } = string.Empty;

                [JsonProperty("value")] public string Value { get; set; } = string.Empty;
            }

            internal class RemoveLongMemoryActionParam
            {
                [JsonProperty("key")] public string Key { get; set; } = string.Empty;
            }
        }
    }
}