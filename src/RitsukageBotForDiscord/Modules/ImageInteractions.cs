using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Graphic;
using RitsukageBot.Library.Graphic.Generators;
using RitsukageBot.Library.Graphic.Processing;
using RitsukageBot.Services.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Discord.Color;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Image interactions
    /// </summary>
    [Group("image", "Image interactions")]
    public class ImageInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Custom ID
        /// </summary>
        public const string CustomId = "image_interaction";

        /// <summary>
        ///     Allowed interactions
        /// </summary>
        public static readonly AllowedInteraction[] AllowedInteractions =
        [
            new("Invert Color", $"{CustomId}:invert_color"),
            new("Invert Frames", $"{CustomId}:invert_frames"),
            new("Distorting Mirror", $"{CustomId}:distorting_mirror"),
            new("Mirror Left", $"{CustomId}:mirror_left"),
            new("Mirror Right", $"{CustomId}:mirror_right"),
            new("Mirror Top", $"{CustomId}:mirror_top"),
            new("Mirror Bottom", $"{CustomId}:mirror_bottom"),
            new("Move Left", $"{CustomId}:move_left"),
            new("Move Right", $"{CustomId}:move_right"),
            new("Move Up", $"{CustomId}:move_up"),
            new("Move Down", $"{CustomId}:move_down"),
        ];

        /// <summary>
        ///     Image cache provider service
        /// </summary>
        public required ImageCacheProviderService ImageCacheProviderService { get; set; }

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<ImageInteractions> Logger { get; set; }

        /// <summary>
        ///     Begin image interaction
        /// </summary>
        /// <param name="url"></param>
        [SlashCommand("modify", "Begin image interaction")]
        public async Task ModifyAsync(string url)
        {
            await DeferAsync().ConfigureAwait(false);

            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);

            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                await FollowupAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            using var imageStream = new MemoryStream();
            var guid = await ImageCacheProviderService.CacheImageAsync(image).ConfigureAwait(false);
            string fileName;
            if (image.Frames.Count > 1)
            {
                await image.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.gif";
            }
            else
            {
                await image.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            await FollowupWithFileAsync(imageStream, fileName, components: GetOperationMenus().Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Good News image
        /// </summary>
        /// <param name="text"></param>
        [SlashCommand("good-news", "Generate Good News image")]
        public async Task GoodNewsAsync(string text)
        {
            await DeferAsync().ConfigureAwait(false);
            using var image = GoodBadNewsGenerators.GenerateGoodNewsImage(text);
            var guid = await ImageCacheProviderService.CacheImageAsync(image).ConfigureAwait(false);
            var fileName = $"{guid}.png";
            using var imageStream = new MemoryStream();
            await image.SaveAsPngAsync(imageStream).ConfigureAwait(false);
            imageStream.Seek(0, SeekOrigin.Begin);
            var component = new ComponentBuilder()
                .WithButton("Publish", $"{CustomId}:cancel_and_publish", ButtonStyle.Success);
            await FollowupWithFileAsync(imageStream, fileName, components: component.Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Bad News image
        /// </summary>
        /// <param name="text"></param>
        [SlashCommand("bad-news", "Generate Bad News image")]
        public async Task BadNewsAsync(string text)
        {
            await DeferAsync().ConfigureAwait(false);
            using var image = GoodBadNewsGenerators.GenerateBadNewsImage(text);
            var guid = await ImageCacheProviderService.CacheImageAsync(image).ConfigureAwait(false);
            var fileName = $"{guid}.png";
            using var imageStream = new MemoryStream();
            await image.SaveAsPngAsync(imageStream).ConfigureAwait(false);
            imageStream.Seek(0, SeekOrigin.Begin);
            var component = new ComponentBuilder()
                .WithButton("Publish", $"{CustomId}:cancel_and_publish", ButtonStyle.Success);
            await FollowupWithFileAsync(imageStream, fileName, components: component.Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Group Cyan image
        /// </summary>
        /// <param name="url"></param>
        [SlashCommand("group-cyan", "Generate Group Cyan image")]
        public async Task GroupCyanAsync(string url)
        {
            await DeferAsync().ConfigureAwait(false);

            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);

            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                await FollowupAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            using var resultImage = GroupCyanImageConvertor.Convert(image);
            image.Dispose();
            var guid = await ImageCacheProviderService.CacheImageAsync(resultImage).ConfigureAwait(false);
            using var imageStream = new MemoryStream();
            string fileName;
            if (resultImage.Frames.Count > 1)
            {
                await resultImage.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.gif";
            }
            else
            {
                await resultImage.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);

            var component = new ComponentBuilder()
                .WithButton("Publish", $"{CustomId}:cancel_and_publish", ButtonStyle.Success);
            await FollowupWithFileAsync(imageStream, fileName, components: component.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Colorful Char image
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fontSize"></param>
        /// <param name="pixelSize"></param>
        [SlashCommand("colorful-chars", "Generate Colorful Chars image")]
        public async Task ColorfulCharImageAsync(string url, int fontSize = 12, int pixelSize = 4)
        {
            await DeferAsync().ConfigureAwait(false);

            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);

            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                await FollowupAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            using var resultImage = ColorfulCharsImageConvertor.Convert(image, fontSize, pixelSize, out _);
            image.Dispose();
            var guid = await ImageCacheProviderService.CacheImageAsync(resultImage).ConfigureAwait(false);
            using var imageStream = new MemoryStream();
            string fileName;
            if (resultImage.Frames.Count > 1)
            {
                await resultImage.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.gif";
            }
            else
            {
                await resultImage.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);

            var component = new ComponentBuilder()
                .WithButton("Publish", $"{CustomId}:cancel_and_publish", ButtonStyle.Success);
            await FollowupWithFileAsync(imageStream, fileName, components: component.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Generate Char image
        /// </summary>
        /// <param name="url"></param>
        /// <param name="pixelSize"></param>
        [SlashCommand("char-image", "Generate Char image")]
        public async Task CharImageAsync(string url, int pixelSize = 4)
        {
            await DeferAsync(true).ConfigureAwait(false);

            var (success, image, message) = await GetImageAsync(url).ConfigureAwait(false);

            if (!success || image is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription(message ?? "Failed to download image")
                    .WithColor(Color.Red)
                    .Build();
                await FollowupAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            var resultImageStr = ColorfulCharsImageConvertor.ConvertToString(image, pixelSize);
            image.Dispose();

            if (resultImageStr.Length > 1992)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Result image is too large to send")
                    .WithColor(Color.Red)
                    .Build();
                await FollowupAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            await FollowupAsync($"```\n{resultImageStr}\n```").ConfigureAwait(false);
        }

        #region Helper methods

        private async Task<(bool, Image<Rgba32>?, string?)> GetImageAsync(string url)
        {
            Image<Rgba32>? image = null;
            string? message = null;
            var success = false;
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

        /// <summary>
        ///     Get operation menus
        /// </summary>
        /// <param name="page"></param>
        /// <param name="isEphemeral"></param>
        /// <returns></returns>
        public static ComponentBuilder GetOperationMenus(int page = 0, bool isEphemeral = true)
        {
            var builder = new ComponentBuilder();
            var interactions = AllowedInteractions.Skip(page * 8).Take(8).ToArray();
            var hasNextPage = AllowedInteractions.Length > (page + 1) * 8;
            var hasLastPage = page > 0;
            switch (interactions.Length)
            {
                case > 4:
                {
                    var row1 = new ActionRowBuilder();
                    for (var i = 0; i < 4; i++) row1.WithButton(interactions[i].AsButtonBuilder());

                    var row2 = new ActionRowBuilder();
                    for (var i = 4; i < interactions.Length; i++) row2.WithButton(interactions[i].AsButtonBuilder());

                    builder.AddRow(row1);
                    builder.AddRow(row2);
                    break;
                }
                case > 0:
                {
                    var row = new ActionRowBuilder();
                    foreach (var interaction in interactions) row.WithButton(interaction.AsButtonBuilder());

                    builder.AddRow(row);
                    break;
                }
            }

            var operationRow = new ActionRowBuilder();
            if (hasLastPage) operationRow.WithButton(LastPageInteraction.AsButtonBuilder());

            if (hasNextPage) operationRow.WithButton(NextPageInteraction.AsButtonBuilder());

            operationRow.WithButton(CancelInteraction.AsButtonBuilder());
            if (isEphemeral) operationRow.WithButton(CancelAndPublishInteraction.AsButtonBuilder());

            builder.AddRow(operationRow);

            return builder;
        }

        /// <summary>
        ///     Allowed interaction
        /// </summary>
        /// <param name="Label"></param>
        /// <param name="Id"></param>
        /// <param name="ButtonStyle"></param>
        /// <param name="Emote"></param>
        public readonly record struct AllowedInteraction(
            string Label,
            string Id,
            ButtonStyle? ButtonStyle = null,
            IEmote? Emote = null)
        {
            /// <summary>
            ///     Label
            /// </summary>
            public string Label { get; init; } = Label;

            /// <summary>
            ///     ID
            /// </summary>
            public string Id { get; init; } = Id;

            /// <summary>
            ///     Button style
            /// </summary>
            public ButtonStyle? ButtonStyle { get; init; } = ButtonStyle;

            /// <summary>
            ///     Emote
            /// </summary>
            public IEmote? Emote { get; init; } = Emote;

            /// <summary>
            ///     As button builder
            /// </summary>
            /// <param name="disabled"></param>
            /// <returns></returns>
            public ButtonBuilder AsButtonBuilder(bool disabled = false)
            {
                var builder = new ButtonBuilder().WithLabel(Label).WithCustomId(Id);
                builder.WithStyle(ButtonStyle ?? Discord.ButtonStyle.Primary);

                if (Emote is not null) builder.WithEmote(Emote);

                builder.WithDisabled(disabled);
                return builder;
            }
        }

        #endregion

        #region Basic interactions

        /// <summary>
        ///     Cancel interaction
        /// </summary>
        public static readonly AllowedInteraction CancelInteraction = new()
        {
            Label = "Cancel",
            Id = $"{CustomId}:cancel",
            ButtonStyle = ButtonStyle.Danger,
        };

        /// <summary>
        ///     Cancel and publish interaction
        /// </summary>
        public static readonly AllowedInteraction CancelAndPublishInteraction = new()
        {
            Label = "Cancel and Publish",
            Id = $"{CustomId}:cancel_and_publish",
            ButtonStyle = ButtonStyle.Danger,
        };

        /// <summary>
        ///     Last page interaction
        /// </summary>
        public static readonly AllowedInteraction LastPageInteraction = new()
        {
            Label = "Last Page",
            Id = $"{CustomId}:last_page",
            ButtonStyle = ButtonStyle.Secondary,
            Emote = new Emoji("⬅️"),
        };

        /// <summary>
        ///     Next page interaction
        /// </summary>
        public static readonly AllowedInteraction NextPageInteraction = new()
        {
            Label = "Next Page",
            Id = $"{CustomId}:next_page",
            ButtonStyle = ButtonStyle.Secondary,
            Emote = new Emoji("➡️"),
        };

        #endregion
    }


    /// <summary>
    ///     Image interaction button
    /// </summary>
    public class ImageInteractionButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<ImageInteractionButton> Logger { get; set; }

        /// <summary>
        ///     Image cache provider service
        /// </summary>
        public required ImageCacheProviderService ImageCacheProviderService { get; set; }

        /// <summary>
        ///     Invert color
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:invert_color")]
        public Task InvertColorAsync()
        {
            return TriggerProcessAsync<InvertColor<Rgba32>>();
        }

        /// <summary>
        ///     Invert frames
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:invert_frames")]
        public Task InvertFrameAsync()
        {
            return TriggerProcessAsync<InvertFrames<Rgba32>>();
        }

        /// <summary>
        ///     Distorting mirror
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction($"{ImageInteractions.CustomId}:distorting_mirror")]
        public Task DistortingMirrorAsync()
        {
            return TriggerProcessAsync<DistortingMirror<Rgba32>>(new());
        }

        /// <summary>
        ///     Mirror left
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:mirror_left")]
        public Task MirrorLeftAsync()
        {
            return TriggerProcessAsync<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Left));
        }

        /// <summary>
        ///     Mirror right
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:mirror_right")]
        public Task MirrorRightAsync()
        {
            return TriggerProcessAsync<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Right));
        }

        /// <summary>
        ///     Mirror top
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:mirror_top")]
        public Task MirrorTopAsync()
        {
            return TriggerProcessAsync<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Top));
        }

        /// <summary>
        ///     Mirror bottom
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:mirror_bottom")]
        public Task MirrorBottomAsync()
        {
            return TriggerProcessAsync<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Bottom));
        }

        /// <summary>
        ///     Move up
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:move_left")]
        public Task MoveLeftAsync()
        {
            return TriggerProcessAsync<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Left));
        }

        /// <summary>
        ///     Move right
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:move_right")]
        public Task MoveRightAsync()
        {
            return TriggerProcessAsync<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Right));
        }

        /// <summary>
        ///     Move up
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:move_up")]
        public Task MoveUpAsync()
        {
            return TriggerProcessAsync<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Up));
        }

        /// <summary>
        ///     Move down
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:move_down")]
        public Task MoveDownAsync()
        {
            return TriggerProcessAsync<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Down));
        }

        #region Helper methods

        private async Task<Image<Rgba32>?> GetImageAsync()
        {
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            if (attachment is null) return null;

            Image<Rgba32>? image = null;
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(attachment.Filename);
                if (Guid.TryParse(fileName, out _))
                    image = await ImageCacheProviderService.GetImageFromGuid(fileName).ConfigureAwait(false);

                image ??= await ImageCacheProviderService.GetImageAsync(attachment.Url).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to download image");
            }
            catch (InvalidImageContentException)
            {
                Logger.LogError("Invalid image content");
            }
            catch (UnknownImageFormatException)
            {
                Logger.LogError("Unknown image format");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to download image");
            }

            return image;
        }

        private async Task SetImageAsync(Image<Rgba32> image)
        {
            var guid = await ImageCacheProviderService.CacheImageAsync(image).ConfigureAwait(false);
            var imageStream = new MemoryStream();
            string fileName;
            if (image.Frames.Count > 1)
            {
                await image.SaveAsGifAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.gif";
            }
            else
            {
                await image.SaveAsPngAsync(imageStream).ConfigureAwait(false);
                fileName = $"{guid}.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();
            await Context.Interaction.Message.ModifyAsync(x =>
                {
                    x.Attachments = new List<FileAttachment> { new(imageStream, fileName) };
                    x.Embed = new EmbedBuilder().WithColor(Color.Green).WithDescription("success").Build();
                })
                .ConfigureAwait(false);
            await imageStream.DisposeAsync().ConfigureAwait(false);
        }

        private Task TriggerProcessAsync<T>() where T : IProcessStep<Rgba32>, new()
        {
            return TriggerProcessAsync(new T());
        }

        private async Task TriggerProcessAsync<T>(T processStep) where T : IProcessStep<Rgba32>
        {
            await Context.Interaction.Message.ModifyAsync(x =>
            {
                x.Embed = new EmbedBuilder().WithColor(Color.Orange).WithDescription("Processing...").Build();
            }).ConfigureAwait(false);
            using var image = await GetImageAsync().ConfigureAwait(false);
            if (image is null) return;

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(processStep);
            var result = await processor.ProcessAsync().ConfigureAwait(false);
            await SetImageAsync(result).ConfigureAwait(false);
        }

        #endregion

        #region Basic interactions

        /// <summary>
        ///     Cancel image interaction
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:cancel")]
        public Task CancelAsync()
        {
            Logger.LogInformation("Image interaction canceled for {MessageId}", Context.Interaction.Message.Id);
            return Context.Interaction.Message.DeleteAsync();
        }

        /// <summary>
        ///     Cancel and publish image interaction
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:cancel_and_publish")]
        public async Task CancelAndPublishAsync()
        {
            Logger.LogInformation("Image interaction canceled and published for {MessageId}",
                Context.Interaction.Message.Id);
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            if (attachment is not null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Image Interaction")
                    .WithImageUrl(attachment.Url)
                    .WithFooter("Published by " + Context.User.Username, Context.User.GetAvatarUrl())
                    .WithTimestamp(DateTimeOffset.Now)
                    .Build();
                await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            }

            await Context.Interaction.Message.DeleteAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Last page
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:last_page")]
        public async Task LastPageAsync()
        {
            var firstComponent = Context.Interaction.Message.Components?.FirstOrDefault()?.Components?.FirstOrDefault();
            if (firstComponent is not null)
            {
                var componentInteraction =
                    ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.Id == firstComponent.CustomId);
                if (componentInteraction.Id is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 - 1;
                    await Context.Interaction
                        .UpdateAsync(x => x.Components = ImageInteractions.GetOperationMenus(page).Build())
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Next page
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.CustomId}:next_page")]
        public async Task NextPageAsync()
        {
            var firstComponent = Context.Interaction.Message.Components?.FirstOrDefault()?.Components?.FirstOrDefault();
            if (firstComponent is not null)
            {
                var componentInteraction =
                    ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.Id == firstComponent.CustomId);
                if (componentInteraction.Id is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 + 1;
                    await Context.Interaction
                        .UpdateAsync(x => x.Components = ImageInteractions.GetOperationMenus(page).Build())
                        .ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}