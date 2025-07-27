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
    }
}