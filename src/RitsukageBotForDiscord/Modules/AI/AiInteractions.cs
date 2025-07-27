using System.Text;
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
        /// <returns></returns>
        [SlashCommand("chat", "Chat with the AI")]
        public async Task ChatAsync(string message)
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

            var role = await GetUserChatTargetRole(Context.User.Id).ConfigureAwait(false);
            if (!ChatClientProvider.GetRoleData(out var roleData, out var temperature, role))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Invalid role: {role}",
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

            var emotes = await GetEmotes().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(emotes))
                roleData.Text += $"""


                                  [Emotes]
                                  You can use the following defined emoticons in your conversations
                                  The format of an emoticon is <:emoticon name:emoticon id>
                                  You can determine the meaning of an emoticon based on its name
                                  When you use emoticons, you must make sure that they are formatted correctly and that you only use the emoticons listed below
                                  list of emoticons:
                                  {emotes}
                                  """;

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

                var assistantMessage =
                    await TryPreprocessingMessage(message, cancellationTokenSource.Token).ConfigureAwait(false);

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
            await BeginChatAsync(messageList, role, 3, temperature, cancellationTokenSource.Token)
                .ConfigureAwait(false);
            lock (LockObject)
            {
                IsProcessing.Remove(Context.User.Id);
            }
        }

        /// <summary>
        ///     Cancel the chat
        /// </summary>
        /// <param name="user"></param>
        [RequireUserPermission(GuildPermission.Administrator
                               | GuildPermission.ManageGuild
                               | GuildPermission.ManageChannels)]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("shutdown_chat", "Shutdown the chat")]
        public async Task ShutdownChatAsync(SocketUser user)
        {
            await DeferAsync().ConfigureAwait(false);
            ShutdownChat(user.Id);
            var embed = new EmbedBuilder
            {
                Title = "Chat with AI",
                Description = $"The chat with AI has been canceled for {user.Mention}",
                Color = Color.DarkGrey,
            };
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Setting the chat role
        /// </summary>
        /// <param name="role"></param>
        [SlashCommand("target_role", "Set the chat role")]
        public async Task SetChatRole([Autocomplete(typeof(AiRolesInteractionAutocompleteHandler))] string role)
        {
            await DeferAsync().ConfigureAwait(false);

            if (!ChatClientProvider.GetRoleData(out _, out _, role))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Invalid role: {role}",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            await SetUserChatTargetRole(Context.User.Id, role).ConfigureAwait(false);
            var embed = new EmbedBuilder
            {
                Title = "Set Chat Role",
                Description = $"The chat role has been set to: {role}",
                Color = Color.Green,
            };
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
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
        ///     Query the good history of the AI
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [SlashCommand("good_history", "Query the good change history of the AI")]
        public async Task QueryGoodHistory(SocketUser? user = null)
        {
            await DeferAsync().ConfigureAwait(false);
            user ??= Context.User;
            var history = await ChatClientProvider.QueryChatDataChangeHistory(user.Id, "good", limit: 10)
                .ConfigureAwait(false);
            var embed = new EmbedBuilder();
            embed.WithCurrentTimestamp();
            embed.WithTitle("Good History");
            embed.WithAuthor(user);
            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            if (history.Length == 0)
            {
                embed.WithDescription("No good change history");
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var item in history)
                {
                    var time = item.Timestamp.ToUnixTimeSeconds();
                    if (string.IsNullOrWhiteSpace(item.Reason))
                        sb.AppendLine($"<t:{time}:R> {item.Value:+#;-#;0}");
                    else
                        sb.AppendLine($"<t:{time}:R> {item.Value:+#;-#;0} ({item.Reason})");
                }

                embed.WithDescription(sb.ToString());
            }

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
            var change = good - userInfo.Good;
            var colorGood = Color.Green;
            var colorBad = Color.Red;
            var rate = (good + 10000) / 20000.0;
            var color = ColorUtility.Transition(colorBad, colorGood, rate);
            embed.WithColor(color);
            userInfo.Good = good;
            await DatabaseProviderService.InsertOrUpdateAsync(userInfo).ConfigureAwait(false);
            await ChatClientProvider
                .RecordChatDataChangeHistory(user.Id, "good", change, "Admin modify", Context.Interaction.CreatedAt)
                .ConfigureAwait(false);
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
                userInfo =
                [
                    .. userInfo.OrderByDescending(x => x.Good)
                        .ThenBy(x => x.Name)
                        .ThenBy(x => x.Id),
                ];

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
        public async Task QueryMemory(SocketUser user, ChatMemoryType type = ChatMemoryType.Any)
        {
            await DeferAsync().ConfigureAwait(false);

            var memoryDict = new Dictionary<ChatMemoryType, JObject>();
            switch (type)
            {
                case ChatMemoryType.Any:
                {
                    foreach (var memoryType in Enum.GetValues<ChatMemoryType>())
                    {
                        if (memoryType == ChatMemoryType.Any) continue;
                        var memory = await ChatClientProvider.GetMemory(user.Id, memoryType).ConfigureAwait(false);
                        if (memoryType == ChatMemoryType.LongTerm)
                        {
                            var longMemory = new JObject();
                            foreach (var (key, value) in memory)
                                if (!key.StartsWith("chat_history_"))
                                    longMemory[key] = value;

                            if (longMemory.Count > 0)
                                memoryDict[memoryType] = longMemory;
                        }
                        else if (memory.Count > 0)
                        {
                            memoryDict[memoryType] = memory;
                        }
                    }

                    break;
                }
                case ChatMemoryType.LongTerm:
                {
                    var memory = new JObject();
                    foreach (var (key, value) in memory)
                        if (!key.StartsWith("chat_history_"))
                            memory[key] = value;
                    if (memory.Count > 0)
                        memoryDict[type] = memory;
                    break;
                }
            }


            if (memoryDict.Count == 0)
            {
                var errorEmbed = new EmbedBuilder();
                errorEmbed.WithAuthor(user);
                errorEmbed.WithTitle("Query Memory");
                errorEmbed.WithDescription("There is no memory for this user");
                errorEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
                errorEmbed.WithCurrentTimestamp();
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle("Query Memory");
            foreach (var (memoryType, memory) in memoryDict)
            {
                var memoryTexts = new List<string>();
                foreach (var memoryItem in memory) memoryTexts.Add($"{memoryItem.Key}: {memoryItem.Value}");
                embed.AddField(memoryType.ToString(), $"```\n{string.Join('\n', memoryTexts)}\n```");
            }

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();
            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Remove the memory of the AI
        /// </summary>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("remove_memory", "Remove the memory of the AI")]
        public async Task RemoveMemory(SocketUser user, string key, ChatMemoryType type = ChatMemoryType.Any)
        {
            await DeferAsync().ConfigureAwait(false);
            var keys = key.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length == 0)
            {
                var errorEmbed = new EmbedBuilder();
                errorEmbed.WithTitle("Error");
                errorEmbed.WithDescription("Please provide the key to remove the memory");
                errorEmbed.WithColor(Color.Red);
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            foreach (var k in keys)
                await ChatClientProvider.RemoveMemory(user.Id, type, k).ConfigureAwait(false);

            var memoryType = type switch
            {
                ChatMemoryType.ShortTerm => "short-term",
                ChatMemoryType.LongTerm => "long-term",
                ChatMemoryType.SelfState => "self-state",
                ChatMemoryType.Any => "any",
                _ => "unknown",
            };
            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle("Remove Memory");
            embed.WithDescription($"The {memoryType} memory has been removed: \n{string.Join('\n', keys)}");
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
                    await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, key.Key)
                        .ConfigureAwait(false);
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
        ///     Clear the context of the AI with short memory
        /// </summary>
        /// <returns></returns>
        [SlashCommand("clear_context_with_short_memory", "Clear the context of the AI with short memory")]
        public async Task ClearContextWithShortMemory()
        {
            await DeferAsync().ConfigureAwait(false);
            var memory = await ChatClientProvider.GetMemory(Context.User.Id, ChatMemoryType.LongTerm)
                .ConfigureAwait(false);
            var keys = new List<string>();
            foreach (var key in memory)
                if (key.Key.StartsWith("chat_history_"))
                {
                    await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, key.Key)
                        .ConfigureAwait(false);
                    keys.Add(key.Key);
                }

            await ChatClientProvider.ClearMemory(Context.User.Id, ChatMemoryType.ShortTerm).ConfigureAwait(false);
            await ChatClientProvider.ClearMemory(Context.User.Id, ChatMemoryType.SelfState).ConfigureAwait(false);

            var embed = new EmbedBuilder();
            embed.WithAuthor(Context.User);
            embed.WithTitle("Clear Context");
            embed.WithDescription(
                $"Cleared all short-term memories\nClear long-term memory context: \n{string.Join('\n', keys)}");
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

        /// <summary>
        ///     Query user ID by username in current Discord server
        /// </summary>
        /// <param name="username">Username to search for</param>
        /// <returns></returns>
        [SlashCommand("query_user_id", "Query user ID by username in current Discord server")]
        public async Task QueryUserIdAsync(string username)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var users = Context.Guild.Users
                .Where(u => u.Username.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                           (u.GlobalName?.Contains(username, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(10)
                .ToList();

            if (!users.Any())
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "User Search",
                    Description = $"No users found with username containing: {username}",
                    Color = Color.Orange,
                };
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder
            {
                Title = "User Search Results",
                Description = $"Found {users.Count} user(s) matching: {username}",
                Color = Color.Green,
            };

            foreach (var user in users)
            {
                var fieldName = user.GlobalName ?? user.Username;
                var fieldValue = $"ID: `{user.Id}`\nUsername: {user.Username}\nMention: <@{user.Id}>";
                embed.AddField(fieldName, fieldValue, true);
            }

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Get user information by user ID in current Discord server
        /// </summary>
        /// <param name="userId">User ID to query</param>
        /// <returns></returns>
        [SlashCommand("get_user_info", "Get user information by user ID in current Discord server")]
        public async Task GetUserInfoAsync(string userId)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            if (!ulong.TryParse(userId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
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
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
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

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     List accessible channels in current Discord server
        /// </summary>
        /// <returns></returns>
        [SlashCommand("list_channels", "List accessible channels in current Discord server")]
        public async Task ListChannelsAsync()
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
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

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Send private message to specified user
        /// </summary>
        /// <param name="userId">User ID to send message to</param>
        /// <param name="message">Message content</param>
        /// <returns></returns>
        [RequireOwner]
        [SlashCommand("send_dm", "Send private message to specified user")]
        public async Task SendDirectMessageAsync(string userId, string message)
        {
            await DeferAsync().ConfigureAwait(false);

            if (!ulong.TryParse(userId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
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
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                await user.SendMessageAsync(message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent private message to {user.Username} (`{user.Id}`)",
                    Color = Color.Green,
                };
                await FollowupAsync(embed: successEmbed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Send message to specified channel
        /// </summary>
        /// <param name="channelId">Channel ID to send message to</param>
        /// <param name="message">Message content</param>
        /// <returns></returns>
        [RequireUserPermission(GuildPermission.Administrator
                               | GuildPermission.ManageGuild
                               | GuildPermission.ManageChannels)]
        [SlashCommand("send_channel_message", "Send message to specified channel")]
        public async Task SendChannelMessageAsync(string channelId, string message)
        {
            await DeferAsync().ConfigureAwait(false);

            if (!ulong.TryParse(channelId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid channel ID format.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
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
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                await channel.SendMessageAsync(message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent message to <#{channel.Id}>",
                    Color = Color.Green,
                };
                await FollowupAsync(embed: successEmbed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Get random users from current channel
        /// </summary>
        /// <param name="count">Number of users to get (max 10)</param>
        /// <returns></returns>
        [SlashCommand("random_users", "Get random users from current channel")]
        public async Task GetRandomUsersAsync(int count = 5)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            if (count <= 0 || count > 10)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Count must be between 1 and 10.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

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
                await FollowupAsync(embed: noUsersEmbed.Build()).ConfigureAwait(false);
                return;
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

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Create AI scheduled task
        /// </summary>
        /// <param name="name">Task name</param>
        /// <param name="prompt">AI prompt</param>
        /// <param name="scheduleTime">When to schedule (format: yyyy-MM-dd HH:mm)</param>
        /// <param name="scheduleType">Schedule type</param>
        /// <param name="channelId">Optional channel ID for output</param>
        /// <param name="intervalMinutes">Interval in minutes for periodic tasks</param>
        /// <param name="targetTimes">Target execution times for countdown tasks</param>
        /// <param name="aiRole">AI role to use</param>
        /// <returns></returns>
        [SlashCommand("create_task", "Create AI scheduled task")]
        public async Task CreateScheduledTaskAsync(
            string name,
            string prompt,
            string scheduleTime,
            [Choice("OneTime", "OneTime")]
            [Choice("Periodic", "Periodic")]
            [Choice("Countdown", "Countdown")]
            [Choice("UntilTime", "UntilTime")]
            string scheduleType = "OneTime",
            string? channelId = null,
            int intervalMinutes = 60,
            int targetTimes = 1,
            [Autocomplete(typeof(AiRolesInteractionAutocompleteHandler))] string? aiRole = null)
        {
            await DeferAsync().ConfigureAwait(false);

            if (!await CheckEnabled().ConfigureAwait(false)) return;

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Parse schedule time
            if (!DateTime.TryParseExact(scheduleTime, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedTime))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid schedule time format. Use: yyyy-MM-dd HH:mm (e.g., 2024-12-25 14:30)",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var scheduleTimeOffset = new DateTimeOffset(parsedTime, TimeZoneInfo.Local.GetUtcOffset(parsedTime));

            // Validate channel ID if provided
            ulong? channelIdParsed = null;
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                if (!ulong.TryParse(channelId, out var cId))
                {
                    var errorEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "Invalid channel ID format.",
                        Color = Color.Red,
                    };
                    await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
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
                    await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }
            }

            // Validate AI role if provided
            if (!string.IsNullOrWhiteSpace(aiRole) && !ChatClientProvider.GetRoleData(out _, out _, aiRole))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Invalid AI role: {aiRole}",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Create task
            var task = new AiScheduledTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                UserId = Context.User.Id,
                GuildId = Context.Guild.Id,
                ChannelId = channelIdParsed,
                Prompt = prompt,
                AiRole = aiRole,
                ScheduleType = scheduleType,
                ScheduleTime = scheduleTimeOffset,
                IntervalSeconds = scheduleType is "Periodic" or "Countdown" or "UntilTime" ? intervalMinutes * 60L : null,
                TargetTimes = scheduleType == "Countdown" ? (ulong)targetTimes : null,
                TargetTime = scheduleType == "UntilTime" ? scheduleTimeOffset.AddMinutes(intervalMinutes * targetTimes) : null,
                IsEnabled = true,
                CreatedTime = DateTimeOffset.UtcNow,
                UpdatedTime = DateTimeOffset.UtcNow
            };

            await DatabaseProviderService.InsertOrUpdateAsync(task).ConfigureAwait(false);

            var embed = new EmbedBuilder
            {
                Title = "AI Scheduled Task Created",
                Description = $"Task **{name}** has been created successfully.",
                Color = Color.Green,
            };

            embed.AddField("Task ID", task.Id, true);
            embed.AddField("Schedule Type", scheduleType, true);
            embed.AddField("Schedule Time", $"<t:{scheduleTimeOffset.ToUnixTimeSeconds()}:F>", true);
            embed.AddField("Output", channelIdParsed.HasValue ? $"<#{channelIdParsed.Value}>" : "Direct Message", true);
            if (!string.IsNullOrWhiteSpace(aiRole))
                embed.AddField("AI Role", aiRole, true);

            embed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     List AI scheduled tasks
        /// </summary>
        /// <param name="showAll">Show all tasks (admin only) or just own tasks</param>
        /// <returns></returns>
        [SlashCommand("list_tasks", "List AI scheduled tasks")]
        public async Task ListScheduledTasksAsync(bool showAll = false)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var query = showAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? "SELECT * FROM AiScheduledTask WHERE guild_id = ? ORDER BY created_time DESC"
                : "SELECT * FROM AiScheduledTask WHERE guild_id = ? AND user_id = ? ORDER BY created_time DESC";

            var parameters = showAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? new object[] { Context.Guild.Id }
                : new object[] { Context.Guild.Id, Context.User.Id };

            var tasks = await DatabaseProviderService.QueryAsync<AiScheduledTask>(query, parameters).ConfigureAwait(false);

            if (!tasks.Any())
            {
                var noTasksEmbed = new EmbedBuilder
                {
                    Title = "AI Scheduled Tasks",
                    Description = showAll ? "No scheduled tasks found in this server." : "You have no scheduled tasks.",
                    Color = Color.Orange,
                };
                await FollowupAsync(embed: noTasksEmbed.Build()).ConfigureAwait(false);
                return;
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

                if (showAll)
                    fieldValue += $"\nCreator: <@{task.UserId}>";

                embed.AddField(fieldName, fieldValue, true);
            }

            if (tasks.Count > 10)
                embed.WithFooter($"Showing 10 of {tasks.Count} tasks");

            embed.WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Delete AI scheduled task
        /// </summary>
        /// <param name="taskId">Task ID to delete</param>
        /// <returns></returns>
        [SlashCommand("delete_task", "Delete AI scheduled task")]
        public async Task DeleteScheduledTaskAsync(string taskId)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(taskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
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
                    Title = "Error",
                    Description = "You don't have permission to delete this task.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: permissionEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Delete the task
            await DatabaseProviderService.DeleteAsync<AiScheduledTask>(taskId).ConfigureAwait(false);

            var successEmbed = new EmbedBuilder
            {
                Title = "Task Deleted",
                Description = $"Successfully deleted task **{task.Name}** (`{task.Id[..8]}...`)",
                Color = Color.Green,
            };

            successEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            successEmbed.WithCurrentTimestamp();

            await FollowupAsync(embed: successEmbed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggle AI scheduled task enabled status
        /// </summary>
        /// <param name="taskId">Task ID to toggle</param>
        /// <returns></returns>
        [SlashCommand("toggle_task", "Toggle AI scheduled task enabled status")]
        public async Task ToggleScheduledTaskAsync(string taskId)
        {
            await DeferAsync().ConfigureAwait(false);

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This command can only be used in a guild.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(taskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
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
                    Title = "Error",
                    Description = "You don't have permission to modify this task.",
                    Color = Color.Red,
                };
                await FollowupAsync(embed: permissionEmbed.Build()).ConfigureAwait(false);
                return;
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

            successEmbed.WithFooter(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrl());
            successEmbed.WithCurrentTimestamp();

            await FollowupAsync(embed: successEmbed.Build()).ConfigureAwait(false);
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