using System.Text;
using Discord;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.AI;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {

        /// <summary>
        /// Begin chat with Function Calling implementation
        /// </summary>
        /// <param name="messageList">Chat messages</param>
        /// <param name="role">Role name</param>
        /// <param name="retry">Retry count</param>
        /// <param name="temperature">Temperature setting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        private async Task BeginChatWithFunctionCallingAsync(IList<ChatMessage> messageList, string role,
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

            Logger.LogInformation("User {UserId} sent a message to chat with AI using Function Calling: {Message}", 
                Context.User.Id, FormatJson(messageList.Last(x => x.Role == ChatRole.User).ToString()));

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
                await TryGettingResponseWithFunctionCalling(messageList, role, endpointConfig, temperature, timeout,
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
                        await TryGettingResponseWithFunctionCalling(messageList, role, endpointConfig, temperature: temperature,
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

        /// <summary>
        /// Try getting response with Function Calling
        /// </summary>
        /// <param name="messageList">Chat messages</param>
        /// <param name="role">Role name</param>
        /// <param name="endpointConfig">Endpoint configuration</param>
        /// <param name="temperature">Temperature</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        private async Task<(bool, string?)> TryGettingResponseWithFunctionCalling(IList<ChatMessage> messageList, string role,
            ChatClientProviderService.EndpointConfig? endpointConfig = null, float temperature = 1.0f,
            long timeout = 60000, CancellationToken cancellationToken = default)
        {
            endpointConfig ??= ChatClientProvider.GetChatEndpointRandomly();
            var chatClient = ChatClientProvider.GetChatClient(endpointConfig);
            
            // Set context for function calling
            AiFunctionCallingService.SetContext(Context.User.Id, Context.Interaction.CreatedAt);
            
            // Get function tools and handlers
            var functions = AiFunctionTools.GetAllFunctions();
            var functionHandlers = AiFunctionCallingService.GetFunctionHandlers();
            
            var sb = new StringBuilder();
            var isCompleted = false;
            var isError = false;
            var isTimeout = false;
            Exception? exception = null;
            var lockObject = new Lock();
            var functionResults = new List<FunctionResultContent>();
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
                            cancellationTokenSource2.Cancel();
                            isTimeout = true;
                            Logger.LogWarning(
                                "It took too long to get a response from {ModelId} in {Url} with role: {Role}",
                                endpointConfig.ModelId, endpointConfig.Endpoint, role);
                        }
                    }, cancellationTokenSource1.Token);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var chatOptions = new ChatOptions
                        {
                            Temperature = temperature,
                            MaxOutputTokens = 8192,
                            Tools = functions,
                            // Properties for Grok
                            AdditionalProperties = new(new Dictionary<string, object?>
                            {
                                { "max_completion_tokens", 8192 },
                                { "search_parameters", new Dictionary<string, object>() },
                            }),
                        };

                        await foreach (var response in chatClient.CompleteStreamingAsync(messageList, chatOptions, cancellationTokenSource2.Token))
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            
                            // Handle function calls (need to move out of lock for async)
                            FunctionCallContent[]? functionCallsToProcess = null;
                            lock (lockObject)
                            {
                                if (response.Contents?.OfType<FunctionCallContent>().Any() == true)
                                {
                                    functionCallsToProcess = response.Contents.OfType<FunctionCallContent>().ToArray();
                                }
                                
                                // Handle text content
                                if (response.Text is { } text && !string.IsNullOrWhiteSpace(text))
                                {
                                    sb.Append(text);
                                }
                            }
                            
                            // Process function calls outside of lock
                            if (functionCallsToProcess is not null)
                            {
                                foreach (var functionCall in functionCallsToProcess)
                                {
                                    if (functionHandlers.TryGetValue(functionCall.Name, out var handler))
                                    {
                                        try
                                        {
                                            var result = await handler(functionCall, cancellationTokenSource2.Token);
                                            
                                            lock (lockObject)
                                            {
                                                functionResults.Add(result);
                                                
                                                // Add function result to message list for continued conversation
                                                messageList.Add(new ChatMessage(ChatRole.Assistant, [functionCall]));
                                                messageList.Add(new ChatMessage(ChatRole.Tool, [result]));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.LogError(ex, "Error executing function {FunctionName}", functionCall.Name);
                                            var errorResult = new FunctionResultContent(functionCall.CallId, functionCall.Name, $"Error: {ex.Message}");
                                            
                                            lock (lockObject)
                                            {
                                                functionResults.Add(errorResult);
                                                messageList.Add(new ChatMessage(ChatRole.Assistant, [functionCall]));
                                                messageList.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        isCompleted = true;
                        await cancellationTokenSource1.CancelAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (isTimeout) return;
                        isError = true;
                        exception = ex;
                        cancellationTokenSource1.Cancel();
                        Logger.LogError(ex,
                            "An error occurred while getting a response from {ModelId} in {Url} with role: {Role}",
                            endpointConfig.ModelId, endpointConfig.Endpoint, role);
                    }
                }, cancellationTokenSource2.Token);
            }

            var responseContent = string.Empty;
            var resultEmbeds = new List<EmbedBuilder>();
            
            try
            {
                while (!isCompleted && !isError && !isTimeout && !cancellationToken.IsCancellationRequested)
                {
                    lock (lockObject)
                    {
                        responseContent = sb.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(responseContent) || functionResults.Count > 0)
                    {
                        // Extract function display embeds
                        if (functionResults.Count > 0)
                        {
                            resultEmbeds = AiFunctionCallingService.ExtractFunctionDisplays(functionResults);
                        }
                        
                        var updateContent = !string.IsNullOrWhiteSpace(responseContent)
                            ? $"|| Generated by {endpointConfig.GetName()} with role: {role} ||\n{responseContent}"
                            : null;
                        
                        await ModifyOriginalResponseAsync(x =>
                        {
                            x.Content = updateContent;
                            var embeds = new List<Embed>();
                            if (resultEmbeds.Count > 0)
                                embeds.AddRange(resultEmbeds.Select(embed => embed.Build()));
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
                await Task.Delay(2000, CancellationToken.None).ContinueWith(async _ =>
                {
                    await ModifyOriginalResponseAsync(x =>
                    {
                        if (resultEmbeds.Count == 0)
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

            responseContent = sb.ToString();
            
            if (cancellationToken.IsCancellationRequested) return (false, "The chat with AI was canceled");

            // Final response formatting - only show the final AI reply, not function calls or thinking
            var finalContent = !string.IsNullOrWhiteSpace(responseContent)
                ? $"|| Generated by {endpointConfig.GetName()} with role: {role} ||\n{responseContent}"
                : null;
            
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = finalContent;
                x.Components = null;
                x.Embeds = resultEmbeds.Count > 0 ? resultEmbeds.Select(embed => embed.Build()).ToArray() : null;
            }).ConfigureAwait(false);

            // Save chat history
            var userMessage = messageList.Last(x => x.Role == ChatRole.User).ToString();
            var userMessageData = JObject.Parse(userMessage);
            var userMessageObject = userMessageData.ToObject<UserMessage>();
            if (userMessageObject is not null && !string.IsNullOrWhiteSpace(responseContent))
            {
                await InsertChatHistory(Context.Interaction.CreatedAt, userMessageObject.Message, responseContent)
                    .ConfigureAwait(false);
            }

            return (true, null);
        }
    }
}