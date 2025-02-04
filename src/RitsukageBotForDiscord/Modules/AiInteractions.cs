using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Ai interactions
    /// </summary>
    [Group("ai", "Ai interactions")]
    public class AiInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Custom ID
        /// </summary>
        public const string CustomId = "ai_interaction";

        private static readonly Dictionary<ulong, CancellationTokenSource> IsProcessing = [];

        private static readonly Lock LockObject = new();

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<AiInteractions> Logger { get; set; }

        /// <summary>
        ///     Chat client provider service
        /// </summary>
        public required ChatClientProviderService ChatClientProviderService { get; set; }

        /// <summary>
        ///     Database provider service
        /// </summary>
        public required DatabaseProviderService DatabaseProviderService { get; set; }

        /// <summary>
        ///     HTTP client factory
        /// </summary>
        public required IHttpClientFactory HttpClientFactory { get; set; }

        /// <summary>
        ///     Configuration
        /// </summary>
        public required IConfiguration Configuration { get; set; }

        /// <summary>
        ///     Shutdown the chat
        /// </summary>
        /// <param name="id"></param>
        public static void ShutdownChat(ulong id)
        {
            lock (LockObject)
            {
                if (!IsProcessing.TryGetValue(id, out var cancellationTokenSource)) return;
                cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        ///     Chat with the AI
        /// </summary>
        /// <param name="message"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [SlashCommand("chat", "Chat with the AI")]
        public async Task ChatAsync(string message,
            [Autocomplete(typeof(AiRolesInteractionAutocompleteHandler))]
            string role = "Normal")
        {
            await DeferAsync().ConfigureAwait(false);

            bool isChatting;
            lock (LockObject)
            {
                isChatting = IsProcessing.ContainsKey(Context.User.Id);
            }

            if (isChatting)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "You are already chatting with the AI",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
            }

            if (ChatClientProviderService.GetRoleData(role) is not { } roleData)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid role",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                return;
            }


            var cancellationTokenSource = new CancellationTokenSource();
            bool valid;
            lock (LockObject)
            {
                valid = IsProcessing.TryAdd(Context.User.Id, cancellationTokenSource);
            }

            if (!valid)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Can not start a new chat with the AI",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                return;
            }

            var messageList = new List<ChatMessage> { roleData };
            if (await BuildUserChatMessage(message).ConfigureAwait(false)
                is not { } userMessage)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "An error occurred while building the user chat message",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                return;
            }

            messageList.Add(userMessage);
            await BeginChatAsync(messageList, false, 3, cancellationTokenSource.Token).ConfigureAwait(false);
            lock (LockObject)
            {
                IsProcessing.Remove(Context.User.Id);
            }
        }

        /// <summary>
        ///     Query the balance of the AI
        ///     Currently, only supports for DeepSeek
        /// </summary>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("balance", "Query the balance of the AI")]
        public async Task QueryBalance()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var endpoint = Configuration.GetValue<string>("AI:Endpoint");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "The AI endpoint is not configured",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var token = Configuration.GetValue<string>("AI:ApiKey");
            if (string.IsNullOrWhiteSpace(token))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "The AI API key is not configured",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var uri = new Uri(endpoint);
            if (uri.Host != "api.deepseek.com")
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "The AI endpoint is currently not supported",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var time = DateTimeOffset.UtcNow;
            var client = HttpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Authorization", "Bearer " + token.Trim());
            var response = await client.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var resultData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jObject = JObject.Parse(resultData);
            if (jObject.TryGetValue("is_available", out var isAvailable) && isAvailable.Value<bool>())
            {
                var balanceInfos = jObject.GetValue("balance_infos");
                if (balanceInfos is not JArray { Count: > 0 })
                {
                    var emptyBalanceEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "The balance information is empty",
                        Color = Color.Red,
                    };
                    await FollowupAsync(embed: emptyBalanceEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                List<EmbedBuilder> embeds = [];
                foreach (var balanceInfo in balanceInfos)
                {
                    var currency = balanceInfo.Value<string>("currency");
                    var totalBalance = balanceInfo.Value<string>("total_balance");
                    var grantedBalance = balanceInfo.Value<string>("granted_balance");
                    var toppedUpBalance = balanceInfo.Value<string>("topped_up_balance");
                    var embed = new EmbedBuilder();
                    if (!string.IsNullOrWhiteSpace(currency))
                        embed.AddField("Currency", currency);

                    if (double.TryParse(totalBalance, out var totalBalanceValue))
                    {
                        switch (totalBalanceValue)
                        {
                            case > 5:
                                embed.WithColor(Color.Green);
                                break;
                            case > 0:
                                embed.WithColor(Color.Orange);
                                break;
                            default:
                                embed.WithColor(Color.Red);
                                break;
                        }

                        embed.AddField("Total Balance", totalBalance);
                    }

                    if (!string.IsNullOrWhiteSpace(grantedBalance))
                        embed.AddField("Granted Balance", grantedBalance);

                    if (!string.IsNullOrWhiteSpace(toppedUpBalance))
                        embed.AddField("Topped Up Balance", toppedUpBalance);

                    embeds.Add(embed);
                }

                await FollowupAsync(embeds: embeds.Select(x
                    => x.WithFooter("DeepSeek", "https://avatars.githubusercontent.com/u/148330874")
                        .WithTimestamp(time)
                        .Build()).ToArray()).ConfigureAwait(false);
            }
            else
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "You currently do not have access to the AI",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Automatically broadcast time
        /// </summary>
        /// <param name="active"></param>
        [RequireUserPermission(GuildPermission.Administrator
                               | GuildPermission.ManageGuild
                               | GuildPermission.ManageChannels)]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("auto_broadcast_time", "Automatically AI broadcast time")]
        public async Task AutoBroadcastTime(bool active = true)
        {
            await DeferAsync().ConfigureAwait(false);
            var channelId = Context.Channel.Id;
            var config = await GetConfigAsync(channelId).ConfigureAwait(false);
            if (active == config.AutomaticallyAiBroadcastTime)
            {
                await FollowupAsync("The setting is already set to the specified value.").ConfigureAwait(false);
                return;
            }

            config.AutomaticallyAiBroadcastTime = active;
            await DatabaseProviderService.InsertOrUpdateAsync(config).ConfigureAwait(false);
            await FollowupAsync($"Automatically AI broadcast time has been {(active ? "enabled" : "disabled")}.")
                .ConfigureAwait(false);
        }

        private async Task<DiscordChannelConfiguration> GetConfigAsync(ulong channelId)
        {
            var (_, config) = await DatabaseProviderService.GetOrCreateAsync<DiscordChannelConfiguration>(channelId)
                .ConfigureAwait(false);
            return config;
        }

        private async Task<ChatMessage?> BuildUserChatMessage(string message)
        {
            var id = Context.User.Id;
            var name = Context.User.Username;
            var time = Context.Interaction.CreatedAt;
            var (_, userInfo) = await DatabaseProviderService.GetOrCreateAsync<ChatUserInformation>(id)
                .ConfigureAwait(false);
            var jObject = new JObject
            {
                ["name"] = name,
                ["message"] = message,
                ["good"] = userInfo.Good,
                ["time"] = time.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            };
            return new(ChatRole.User, jObject.ToString());
        }

        private async Task BeginChatAsync(IList<ChatMessage> messageList, bool useTools = false, int retry = 0,
            CancellationToken cancellationToken = default)
        {
            if (!CheckUserInputMessage(messageList))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("User {UserId} sent a message to chat with AI: {Message}", Context.User.Id,
                messageList.Last(x => x.Role == ChatRole.User).ToString());

            var component = new ComponentBuilder();
            component.WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger);

            var waitEmbed = new EmbedBuilder();
            waitEmbed.WithTitle("Chatting with AI");
            waitEmbed.WithDescription("Getting response from the AI...");
            waitEmbed.WithColor(Color.Orange);

            await FollowupAsync(embed: waitEmbed.Build(), components: component.Build()).ConfigureAwait(false);

            var (isSuccess, errorMessage) =
                await TryGettingResponse(messageList, useTools, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            if (isSuccess) return;
            if (cancellationToken.IsCancellationRequested) return;

            if (retry > 0 && !cancellationToken.IsCancellationRequested)
                for (var i = 0; i < retry; i++)
                {
                    var retryMessage = $"{errorMessage}\nRetrying... ({i + 1}/{retry})";
                    var retryEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = retryMessage,
                        Color = Color.Red,
                    };
                    await ModifyOriginalResponseAsync(x => x.Embed = retryEmbed.Build()).ConfigureAwait(false);
                    (isSuccess, errorMessage) =
                        await TryGettingResponse(messageList, useTools, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    if (isSuccess) return;
                    if (cancellationToken.IsCancellationRequested) return;
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

        private async Task<(bool, string?)> TryGettingResponse(IList<ChatMessage> messageList, bool useTools = false,
            long timeout = 60000, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            var haveContent = false;
            var checkedEmbed = false;
            var isCompleted = false;
            var isUpdated = false;
            var isError = false;
            var isTimeout = false;
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
                            Logger.LogWarning("The chat with AI took too long to respond");
                        }
                    }, cancellationTokenSource1.Token);
                _ = Task.Run(async () =>
                    {
                        await foreach (var response in ChatClientProviderService.CompleteStreamingAsync(messageList,
                                           useTools,
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
                        cancellationTokenSource1.Cancel();
                        Logger.LogError(x.Exception, "Error while processing the chat with AI tools");
                    }, cancellationTokenSource2.Token);
            }

            try
            {
                while (!isCompleted && !isError && !isTimeout && !cancellationToken.IsCancellationRequested)
                {
                    string? updatingContent = null;
                    lock (lockObject)
                    {
                        if (isUpdated)
                        {
                            (_, updatingContent, _) = ChatClientProviderService.FormatResponse(sb.ToString());
                            isUpdated = false;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(updatingContent))
                    {
                        if (checkedEmbed)
                        {
                            await ModifyOriginalResponseAsync(x => x.Content = updatingContent).ConfigureAwait(false);
                        }
                        else
                        {
                            checkedEmbed = true;
                            await ModifyOriginalResponseAsync(x =>
                            {
                                x.Content = updatingContent;
                                x.Embed = null;
                            }).ConfigureAwait(false);
                        }
                    }

                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                return (false, "The chat with AI was canceled");
            }

            if (isError) return (false, "An error occurred while processing the chat with AI tools");
            if (isTimeout) return (false, "The chat with AI tools took too long to respond");
            if (cancellationToken.IsCancellationRequested) return (false, "The chat with AI was canceled");

            var (hasJsonHeader, content, jsonHeader) = ChatClientProviderService.FormatResponse(sb.ToString());
            if (hasJsonHeader)
                try
                {
                    var jObject = JObject.Parse(jsonHeader!);
                    var current = jObject.TryGetValue("good", out var good) ? good.Value<int>() : 0;
                    var before = jObject.TryGetValue("before", out var beforeValue) ? beforeValue.Value<int>() : 0;
                    var change = jObject.TryGetValue("change", out var changeValue) ? changeValue.Value<int>() : 0;
                    var reason = jObject.TryGetValue("reason", out var reasonValue)
                        ? reasonValue.Value<string>()
                        : null;
                    var (_, userInfo) = await DatabaseProviderService
                        .GetOrCreateAsync<ChatUserInformation>(Context.User.Id)
                        .ConfigureAwait(false);
                    if (string.IsNullOrEmpty(reason))
                        Logger.LogInformation("User {UserId} got {Change} points, {Before} -> {Current}",
                            Context.User.Id, change, before, current);
                    else
                        Logger.LogInformation("User {UserId} got {Change} points, {Before} -> {Current} ({Reason})",
                            Context.User.Id, change, before, current, reason);
                    userInfo.Good = current;
                    await DatabaseProviderService.InsertOrUpdateAsync(userInfo).ConfigureAwait(false);
                    if (change != 0)
                    {
                        var embed = new EmbedBuilder();
                        embed.WithColor(change > 0 ? Color.Green : Color.Red);
                        var description = change > 0
                            ? $"Increased by {change} points, current points: {current}"
                            : $"Decreased by {Math.Abs(change)} points, current points: {current}";
                        embed.WithDescription(!string.IsNullOrWhiteSpace(reason)
                            ? $"{description} ({reason})"
                            : description);
                        await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    }
                    else
                    {
                        await ModifyOriginalResponseAsync(x => x.Embed = null).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error while parsing the JSON header");
                }

            if (!string.IsNullOrWhiteSpace(content))
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = content;
                    x.Components = null;
                }).ConfigureAwait(false);

            return (true, null);
        }

        private static bool CheckUserInputMessage(IList<ChatMessage> messageList)
        {
            string? userInputMessage = null;
            var lastUserMessage = messageList.LastOrDefault(x => x.Role == ChatRole.User);
            if (lastUserMessage is not null)
                userInputMessage = lastUserMessage.ToString();
            return !string.IsNullOrWhiteSpace(userInputMessage);
        }

        /// <summary>
        ///     AI interaction autocomplete handler
        /// </summary>
        public class AiRolesInteractionAutocompleteHandler : AutocompleteHandler
        {
            /// <summary>
            ///     Chat client provider service
            /// </summary>
            public required ChatClientProviderService ChatClientProviderService { get; set; }

            /// <summary>
            ///     Generate suggestions
            /// </summary>
            /// <param name="context"></param>
            /// <param name="autocompleteInteraction"></param>
            /// <param name="parameter"></param>
            /// <param name="services"></param>
            /// <returns></returns>
            public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
            {
                var results = ChatClientProviderService.GetRoles().Select(x => new AutocompleteResult(x, x));
                return Task.FromResult(AutocompletionResult.FromSuccess(results.Take(25)));
            }
        }
    }

    /// <summary>
    ///     AI interaction button
    /// </summary>
    public class AiInteractionButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<AiInteractionButton> Logger { get; set; }

        /// <summary>
        ///     Cancel the chat
        /// </summary>
        [ComponentInteraction($"{AiInteractions.CustomId}:cancel_chat")]
        public Task CancelAsync()
        {
            Logger.LogInformation("Ai chat interaction canceled for {MessageId}", Context.Interaction.Message.Id);
            AiInteractions.ShutdownChat(Context.User.Id);
            var embed = new EmbedBuilder
            {
                Title = "Chat with AI",
                Description = "The chat with AI was canceled",
                Color = Color.DarkGrey,
            };
            return Context.Interaction.UpdateAsync(x =>
            {
                x.Content = null;
                x.Embed = embed.Build();
                x.Components = null;
            });
        }
    }
}