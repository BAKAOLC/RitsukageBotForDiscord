using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.AI
{
    /// <summary>
    ///     Ai interactions
    /// </summary>
    [Group("ai", "Ai interactions")]
    public partial class AiInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
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
        public required ChatClientProviderService ChatClientProvider { get; set; }

        /// <summary>
        ///     Database provider service
        /// </summary>
        public required DatabaseProviderService DatabaseProviderService { get; set; }

        /// <summary>
        ///     HTTP client factory
        /// </summary>
        public required IHttpClientFactory HttpClientFactory { get; set; }

        /// <summary>
        ///     Bili kernel provider service
        /// </summary>
        public required BiliKernelProviderService BiliKernelProviderService { get; set; }

        /// <summary>
        ///     Google API
        /// </summary>
        public required GoogleSearchProviderService GoogleSearchProviderService { get; set; }

        /// <summary>
        ///     Shutdown the chat
        /// </summary>
        /// <param name="id"></param>
        public static void ShutdownChat(ulong id)
        {
            lock (LockObject)
            {
                if (!IsProcessing.Remove(id, out var cancellationTokenSource)) return;
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

            if (!await CheckEnabled().ConfigureAwait(false)) return;

            bool isChatting;
            lock (LockObject)
            {
                isChatting = IsProcessing.TryGetValue(Context.User.Id, out var value)
                             && !value.IsCancellationRequested;
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

            if (!ChatClientProvider.GetRoleData(out var roleData, out var temperature, role))
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
            var showBtn = true;
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

            if (ChatClientProvider.CheckAssistantEnabled("Preprocessing"))
            {
                var assistantEmbed = new EmbedBuilder();
                assistantEmbed.WithDescription("Preparing the chat with the AI... Please wait...");
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embed = assistantEmbed.Build();
                    x.Components = new ComponentBuilder()
                        .WithButton("Cancel", $"{CustomId}:cancel_chat", ButtonStyle.Danger).Build();
                }).ConfigureAwait(false);

                showBtn = false;

                var assistantMessage =
                    await TryPreprocessMessage(message, cancellationTokenSource.Token).ConfigureAwait(false);

                if (cancellationTokenSource.IsCancellationRequested) return;

                if (!string.IsNullOrWhiteSpace(assistantMessage))
                {
                    Logger.LogInformation("Assistant message: {AssistantMessage}", assistantMessage);
                    roleData.Text += "\n\n" + ChatClientProvider.FormatAssistantMessage(assistantMessage);
                }
            }

            if (cancellationTokenSource.IsCancellationRequested) return;
            if (await ChatClientProvider.BuildUserChatMessage(Context.User.Username, Context.User.Id,
                    Context.Interaction.CreatedAt, message).ConfigureAwait(false)
                is not { } userMessage)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "An error occurred while building the user chat message",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embed = embed.Build();
                }).ConfigureAwait(false);
                return;
            }

            messageList.Add(userMessage);

            if (cancellationTokenSource.IsCancellationRequested) return;
            await BeginChatAsync(messageList, role, 3, temperature, showBtn, cancellationTokenSource.Token)
                .ConfigureAwait(false);
            lock (LockObject)
            {
                IsProcessing.Remove(Context.User.Id);
            }
        }

        /// <summary>
        ///     Query the goods of the AI
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [SlashCommand("goods", "Query the goods of the AI")]
        public async Task QueryGoods(SocketUser? user = null)
        {
            await DeferAsync().ConfigureAwait(false);

            user ??= Context.User;
            var (_, userInfo) = await DatabaseProviderService.GetOrCreateAsync<ChatUserInformation>(user.Id)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithCurrentTimestamp();
            embed.WithTitle("User Good");
            embed.AddField("Good", userInfo.Good.ToString());
            embed.WithAuthor(user);
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            var colorGood = Color.Green;
            var colorBad = Color.Red;
            var rate = (userInfo.Good + 10000) / 20000.0;
            var color = ColorUtility.Transition(colorBad, colorGood, rate);
            embed.WithColor(color);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Modify the user's goods
        /// </summary>
        /// <param name="good"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("modify_goods", "Modify the user's goods")]
        public async Task ModifyUserGoods(SocketUser user, int good)
        {
            await DeferAsync().ConfigureAwait(false);
            var (_, userInfo) = await DatabaseProviderService.GetOrCreateAsync<ChatUserInformation>(user.Id)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithCurrentTimestamp();
            embed.WithTitle("User Good");
            embed.WithDescription("The user's good has been modified");
            embed.AddField("From", userInfo.Good.ToString());
            embed.AddField("To", good.ToString());
            embed.WithAuthor(user);
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            var colorGood = Color.Green;
            var colorBad = Color.Red;
            var rate = (good + 10000) / 20000.0;
            var color = ColorUtility.Transition(colorBad, colorGood, rate);
            embed.WithColor(color);
            userInfo.Good = good;
            await DatabaseProviderService.InsertOrUpdateAsync(userInfo).ConfigureAwait(false);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Show the good rank
        /// </summary>
        /// <returns></returns>
        [SlashCommand("show_good_rank", "Show the good rank")]
        public async Task ShowGoodRank()
        {
            await DeferAsync().ConfigureAwait(false);

            var table = DatabaseProviderService.Table<ChatUserInformation>();
            var count = await table.CountAsync().ConfigureAwait(false);
            var timestamp = DateTimeOffset.UtcNow;

            if (count < 20)
            {
                var users = await table.ToArrayAsync().ConfigureAwait(false);
                var userInfo = await GetUserGoodInfoAsync(users).ConfigureAwait(false);
                userInfo = userInfo.OrderByDescending(x => x.Good)
                    .ThenBy(x => x.Name)
                    .ThenBy(x => x.Id)
                    .ToArray();

                var lines = new string[userInfo.Length];
                for (var i = 0; i < userInfo.Length; i++)
                {
                    var user = userInfo[i];
                    lines[i] = $"{i + 1,2}. <@!{user.Id}> `{user.Good}`";
                }

                var totalEmbed = new EmbedBuilder();
                totalEmbed.WithTitle("Good Rank");
                totalEmbed.WithDescription(string.Join('\n', lines));
                totalEmbed.WithColor(Color.Green);
                totalEmbed.WithTimestamp(timestamp);
                totalEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());

                await FollowupAsync(embed: totalEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var highUsers = await table.OrderByDescending(x => x.Good)
                .ThenBy(x => x.Id)
                .Take(10)
                .ToArrayAsync()
                .ConfigureAwait(false);
            var lowUsers = await table.OrderBy(x => x.Good)
                .ThenBy(x => x.Id)
                .Take(10)
                .ToArrayAsync()
                .ConfigureAwait(false);
            var highUserInfo = await GetUserGoodInfoAsync(highUsers).ConfigureAwait(false);
            var lowUserInfo = await GetUserGoodInfoAsync(lowUsers).ConfigureAwait(false);
            var highLines = new string[highUserInfo.Length];
            for (var i = 0; i < highUserInfo.Length; i++)
            {
                var user = highUserInfo[i];
                highLines[i] = $"{i + 1,2}. <@!{user.Id}> `{user.Good}`";
            }

            var lowLines = new string[lowUserInfo.Length];
            for (var i = 0; i < lowUserInfo.Length; i++)
            {
                var user = lowUserInfo[i];
                lowLines[i] = $"{i + 1,2}. <@!{user.Id}> `{user.Good}`";
            }

            var highEmbed = new EmbedBuilder();
            highEmbed.WithTitle("Good Rank");
            highEmbed.WithDescription(string.Join('\n', highLines));
            highEmbed.WithColor(Color.Green);
            highEmbed.WithTimestamp(timestamp);
            highEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            var lowEmbed = new EmbedBuilder();
            lowEmbed.WithTitle("Bad Rank");
            lowEmbed.WithDescription(string.Join('\n', lowLines));
            lowEmbed.WithColor(Color.Red);
            lowEmbed.WithTimestamp(timestamp);
            lowEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            await FollowupAsync(embeds: [highEmbed.Build(), lowEmbed.Build()]).ConfigureAwait(false);
        }

        /// <summary>
        ///     Query the balance of the AI
        ///     Currently, only supports for DeepSeek
        /// </summary>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("balance", "Query the balance of the AI")]
        internal async Task QueryBalance()
        {
            await DeferAsync(true).ConfigureAwait(false);

            var configs = ChatClientProvider.GetEndpointConfigs();
            var deepSeekConfig = configs.FirstOrDefault(x => new Uri(x.Endpoint).Host == "api.deepseek.com");
            if (deepSeekConfig is null)
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
            request.Headers.Add("Authorization", "Bearer " + deepSeekConfig.ApiKey.Trim());
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

                await FollowupAsync(embeds:
                [
                    .. embeds.Select(x
                        => x.WithFooter("DeepSeek", "https://avatars.githubusercontent.com/u/148330874")
                            .WithTimestamp(time)
                            .Build()),
                ]).ConfigureAwait(false);
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
        ///     Query the short memory of the AI
        /// </summary>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("query_memory", "Query the memory of the AI")]
        public async Task QueryMemory(SocketUser user, ChatMemoryType type = ChatMemoryType.ShortTerm)
        {
            await DeferAsync().ConfigureAwait(false);
            user ??= Context.User;
            var memory = await ChatClientProvider.GetMemory(user.Id, type).ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle(type == ChatMemoryType.ShortTerm ? "Short Memory" : "Long Memory");
            var memoryTexts = new List<string>();
            foreach (var memoryItem in memory) memoryTexts.Add($"{memoryItem.Key}: {memoryItem.Value}");
            embed.WithDescription($"```\n{string.Join('\n', memoryTexts)}\n```");
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Remove the memory of the AI
        /// </summary>
        /// <param name="user"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("remove_memory", "Remove the memory of the AI")]
        public async Task RemoveMemory(SocketUser user, ChatMemoryType type = ChatMemoryType.ShortTerm,
            params string[] key)
        {
            await DeferAsync().ConfigureAwait(false);
            foreach (var k in key)
                await ChatClientProvider.RemoveMemory(user.Id, type, k);
            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle("Remove Memory");
            embed.WithDescription($"The memory has been removed: \n{string.Join('\n', key)}");
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();
            embed.WithColor(Color.DarkRed);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Clear the context of the AI
        /// </summary>
        /// <returns></returns>
        [SlashCommand("clear_context", "Clear the context of the AI")]
        public async Task ClearContext()
        {
            await DeferAsync().ConfigureAwait(false);
            var memory = await ChatClientProvider.GetMemory(Context.User.Id, ChatMemoryType.LongTerm)
                .ConfigureAwait(false);
            var keys = new List<string>();
            foreach (var key in memory)
                if (key.Key.StartsWith("chat_history_"))
                {
                    await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, key.Key);
                    keys.Add(key.Key);
                }

            var embed = new EmbedBuilder();
            embed.WithAuthor(Context.User);
            embed.WithTitle("Clear Context");
            embed.WithDescription($"Clear long-term memory context: \n{string.Join('\n', keys)}");
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();
            embed.WithColor(Color.DarkRed);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Remove all memories of the AI
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("remove_all_memories", "Remove all memories of the AI")]
        public async Task RemoveAllMemories(SocketUser user)
        {
            await DeferAsync().ConfigureAwait(false);

            var shortCount = await ChatClientProvider.ClearMemory(user.Id, ChatMemoryType.ShortTerm)
                .ConfigureAwait(false);
            var longCount = await ChatClientProvider.ClearMemory(user.Id, ChatMemoryType.LongTerm)
                .ConfigureAwait(false);

            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle("Remove All Memories");
            embed.WithDescription("The memories have been removed.");
            embed.AddField("Short Term", shortCount.ToString());
            embed.AddField("Long Term", longCount.ToString());
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();
            embed.WithColor(Color.DarkRed);
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
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
            AiInteractions.ShutdownChat(Context.Interaction.Message.Interaction.User.Id);
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