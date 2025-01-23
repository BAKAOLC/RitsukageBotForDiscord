using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Graphic.Generators;
using RitsukageBot.Services.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Discord.Color;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Proxy command for image interactions
    /// </summary>
    [Group("image")]
    public class ImageInteractionsCommandProxy : ModuleBase<SocketCommandContext>
    {
        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<ImageInteractionsCommandProxy> Logger { get; set; }

        /// <summary>
        ///     Image cache provider service
        /// </summary>
        public required ImageCacheProviderService ImageCacheProviderService { get; set; }

        /// <summary>
        ///     Begin image interaction
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [Command("modify")]
        [Summary("Modify an image")]
        public async Task ModifyImageAsync([Summary("The image to modify")] string? url = null)
        {
            var content = $"Executing [!image modify] by {Context.User.Mention}";
            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);
            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                var errorComponent = new ComponentBuilder()
                    .WithButton("Cancel", $"{ImageInteractions.CustomId}:cancel", ButtonStyle.Danger);
                await ReplyAsync(content, embed: errorEmbed, components: errorComponent.Build(), allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                return;
            }

            using var imageStream = new MemoryStream();
            string fileName;
            if (image.Frames.Count > 1)
            {
                await image.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = "image.gif";
            }
            else
            {
                await image.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = "image.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            await Context.Channel.SendFileAsync(imageStream, fileName, content,
                components: ImageInteractions.GetOperationMenus(isEphemeral: false).Build(),
                allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Group Cyan image
        /// </summary>
        /// <param name="url"></param>
        [Command("group-cyan")]
        [Summary("Generate Group Cyan image")]
        public async Task GroupCyanAsync([Summary("The image to modify")] string? url = null)
        {
            var content = $"Executing [!image group-cyan] by {Context.User.Mention}";
            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);
            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                var errorComponent = new ComponentBuilder()
                    .WithButton("Cancel", $"{ImageInteractions.CustomId}:cancel", ButtonStyle.Danger);
                await ReplyAsync(content, embed: errorEmbed, components: errorComponent.Build(), allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                return;
            }

            using var resultImage = GroupCyanImageConvertor.Convert(image);
            image.Dispose();
            using var imageStream = new MemoryStream();
            string fileName;
            if (resultImage.Frames.Count > 1)
            {
                await resultImage.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = "image.gif";
            }
            else
            {
                await resultImage.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = "image.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            var component = new ComponentBuilder()
                .WithButton("Cancel", $"{ImageInteractions.CustomId}:cancel", ButtonStyle.Danger)
                .WithButton("Cancel and Publish", $"{ImageInteractions.CustomId}:cancel_and_publish",
                    ButtonStyle.Success);
            await Context.Channel.SendFileAsync(imageStream, fileName, content, components: component.Build(),
                allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Colorful Chars image
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fontSize"></param>
        /// <param name="pixelSize"></param>
        [Command("colorful-chars")]
        [Summary("Generate Colorful Chars image")]
        public async Task ColorfulCharImageAsync([Summary("The image to modify")] string? url = null,
            [Summary("char scale")] int fontSize = 12, [Summary("get char for every X pixel")] int pixelSize = 4)
        {
            var content = $"Executing [!image colorful-chars] by {Context.User.Mention}";
            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);
            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                var errorComponent = new ComponentBuilder()
                    .WithButton("Cancel", $"{ImageInteractions.CustomId}:cancel", ButtonStyle.Danger);
                await ReplyAsync(content, embed: errorEmbed, components: errorComponent.Build(), allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                return;
            }

            using var resultImage = ColorfulCharsImageConvertor.Convert(image, fontSize, pixelSize, out _);
            image.Dispose();
            using var imageStream = new MemoryStream();
            string fileName;
            if (resultImage.Frames.Count > 1)
            {
                await resultImage.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = "image.gif";
            }
            else
            {
                await resultImage.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = "image.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            var component = new ComponentBuilder()
                .WithButton("Cancel", $"{ImageInteractions.CustomId}:cancel", ButtonStyle.Danger)
                .WithButton("Cancel and Publish", $"{ImageInteractions.CustomId}:cancel_and_publish",
                    ButtonStyle.Success);
            await Context.Channel.SendFileAsync(imageStream, fileName, content, components: component.Build(),
                allowedMentions: new()
                {
                    UserIds = [Context.User.Id],
                }).ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        private async Task<(bool, Image<Rgba32>?, string?)> GetImageAsync(string? url)
        {
            Image<Rgba32>? image = null;
            string? message = null;
            var success = false;

            if (string.IsNullOrWhiteSpace(url))
            {
                IAttachment? attachment =
                    Context.Message.Attachments.FirstOrDefault(x => x.ContentType.StartsWith("image/"));
                if (attachment is not null)
                {
                    url = attachment.Url;
                }
                else if (Context.Message.ReferencedMessage is not null)
                {
                    attachment =
                        Context.Message.ReferencedMessage.Attachments.FirstOrDefault(x =>
                            x.ContentType.StartsWith("image/"));
                    if (attachment is not null)
                        url = attachment.Url;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                message = "No image provided";
                return (false, null, message);
            }

            try
            {
                image = await ImageCacheProviderService.GetImageAsync(url).ConfigureAwait(false);
                success = true;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to download image");
                message = ex.Message;
            }
            catch (NotSupportedException ex)
            {
                Logger.LogError(ex, "Failed to download image");
                message = ex.Message;
            }
            catch (InvalidImageContentException)
            {
                Logger.LogError("Invalid image content");
                message = "Invalid image content";
            }
            catch (UnknownImageFormatException)
            {
                Logger.LogError("Unknown image format");
                message = "Unknown image format";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to download image");
                message = ex.Message;
            }

            return (success, image, message);
        }
    }
}