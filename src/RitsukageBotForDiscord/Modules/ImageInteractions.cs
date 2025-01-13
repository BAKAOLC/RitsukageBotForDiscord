using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Graphic;
using RitsukageBot.Library.Graphic.Processing;
using RitsukageBot.Services.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Image interactions
    /// </summary>
    [Group("image", "Image interactions")]
    public class ImageInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Tag custom ID
        /// </summary>
        public const string TagCustomId = "image_interaction";

        /// <summary>
        ///     Allowed interactions
        /// </summary>
        public static readonly AllowedInteraction[] AllowedInteractions =
        [
            new("Invert Color", $"{TagCustomId}:invert_color"),
            new("Invert Frames", $"{TagCustomId}:invert_frames"),
            new("Mirror Left", $"{TagCustomId}:mirror_left"),
            new("Mirror Right", $"{TagCustomId}:mirror_right"),
            new("Mirror Top", $"{TagCustomId}:mirror_top"),
            new("Mirror Bottom", $"{TagCustomId}:mirror_bottom"),
            new("Move Left", $"{TagCustomId}:move_left"),
            new("Move Right", $"{TagCustomId}:move_right"),
            new("Move Up", $"{TagCustomId}:move_up"),
            new("Move Down", $"{TagCustomId}:move_down"),
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
        public async Task ImageAsync(string url)
        {
            await DeferAsync(true).ConfigureAwait(false);
            Image<Rgba32>? image = null;
            var message = string.Empty;
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

            if (!success || image is null)
            {
                await FollowupAsync(string.IsNullOrEmpty(message) ? "Failed to download image" : message)
                    .ConfigureAwait(false);
                return;
            }

            var imageStream = new MemoryStream();
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

            await FollowupWithFileAsync(imageStream, fileName, components: GetOperationMenus().Build())
                .ConfigureAwait(false);
        }

        #region Helper methods

        /// <summary>
        ///     Get operation menus
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static ComponentBuilder GetOperationMenus(int page = 0)
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
            operationRow.WithButton(CancelAndPublishInteraction.AsButtonBuilder());
            builder.AddRow(operationRow);

            return builder;
        }

        /// <summary>
        ///     Allowed interaction
        /// </summary>
        /// <param name="Label"></param>
        /// <param name="CustomId"></param>
        /// <param name="ButtonStyle"></param>
        /// <param name="Emote"></param>
        public readonly record struct AllowedInteraction(
            string Label,
            string CustomId,
            ButtonStyle? ButtonStyle = null,
            IEmote? Emote = null)
        {
            /// <summary>
            ///     Label
            /// </summary>
            public string Label { get; init; } = Label;

            /// <summary>
            ///     Custom ID
            /// </summary>
            public string CustomId { get; init; } = CustomId;

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
                var builder = new ButtonBuilder().WithLabel(Label).WithCustomId(CustomId);
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
            CustomId = $"{TagCustomId}:cancel",
            ButtonStyle = ButtonStyle.Danger,
        };

        /// <summary>
        ///     Cancel and publish interaction
        /// </summary>
        public static readonly AllowedInteraction CancelAndPublishInteraction = new()
        {
            Label = "Cancel and Publish",
            CustomId = $"{TagCustomId}:cancel_and_publish",
            ButtonStyle = ButtonStyle.Danger,
        };

        /// <summary>
        ///     Last page interaction
        /// </summary>
        public static readonly AllowedInteraction LastPageInteraction = new()
        {
            Label = "Last Page",
            CustomId = $"{TagCustomId}:last_page",
            ButtonStyle = ButtonStyle.Secondary,
            Emote = new Emoji("⬅️"),
        };

        /// <summary>
        ///     Next page interaction
        /// </summary>
        public static readonly AllowedInteraction NextPageInteraction = new()
        {
            Label = "Next Page",
            CustomId = $"{TagCustomId}:next_page",
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
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:invert_color")]
        public Task InvertColorAsync()
        {
            return TriggerProcess<InvertColor<Rgba32>>();
        }

        /// <summary>
        ///     Invert frames
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:invert_frames")]
        public Task InvertFrameAsync()
        {
            return TriggerProcess<InvertFrames<Rgba32>>();
        }

        /// <summary>
        ///     Mirror left
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_left")]
        public Task MirrorLeftAsync()
        {
            return TriggerProcess<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Left));
        }

        /// <summary>
        ///     Mirror right
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_right")]
        public Task MirrorRightAsync()
        {
            return TriggerProcess<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Right));
        }

        /// <summary>
        ///     Mirror top
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_top")]
        public Task MirrorTopAsync()
        {
            return TriggerProcess<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Top));
        }

        /// <summary>
        ///     Mirror bottom
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_bottom")]
        public Task MirrorBottomAsync()
        {
            return TriggerProcess<HalfMirror<Rgba32>>(new(HalfMirror<Rgba32>.MirrorType.Bottom));
        }

        /// <summary>
        ///     Move up
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:move_left")]
        public Task MoveLeftAsync()
        {
            return TriggerProcess<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Left));
        }

        /// <summary>
        ///     Move right
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:move_right")]
        public Task MoveRightAsync()
        {
            return TriggerProcess<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Right));
        }

        /// <summary>
        ///     Move up
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:move_up")]
        public Task MoveUpAsync()
        {
            return TriggerProcess<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Up));
        }

        /// <summary>
        ///     Move down
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:move_down")]
        public Task MoveDownAsync()
        {
            return TriggerProcess<MoveAnimation<Rgba32>>(new(MoveAnimation<Rgba32>.MoveDirection.Down));
        }

        #region Helper methods

        private async Task<Image<Rgba32>?> GetImage()
        {
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            if (attachment is null) return null;

            Image<Rgba32>? image = null;
            try
            {
                image = await ImageCacheProviderService.GetImageAsync(attachment.Url).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to download image");
            }
            catch (NotSupportedException ex)
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

        private async Task SetImage(Image<Rgba32> image)
        {
            var imageStream = new MemoryStream();
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

            await Context.Interaction
                .UpdateAsync(x => x.Attachments = new List<FileAttachment> { new(imageStream, fileName) })
                .ConfigureAwait(false);
        }

        private Task TriggerProcess<T>() where T : IProcessStep<Rgba32>, new()
        {
            return TriggerProcess(new T());
        }

        private async Task TriggerProcess<T>(T processStep) where T : IProcessStep<Rgba32>
        {
            var image = await GetImage().ConfigureAwait(false);
            if (image is null) return;

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(processStep);
            var result = await processor.ProcessAsync().ConfigureAwait(false);
            await SetImage(result).ConfigureAwait(false);
        }

        #endregion

        #region Basic interactions

        /// <summary>
        ///     Cancel image interaction
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:cancel")]
        public Task CancelAsync()
        {
            Logger.LogInformation("Image interaction canceled");
            return Context.Interaction.UpdateAsync(x => x.Components = null);
        }

        /// <summary>
        ///     Cancel and publish image interaction
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:cancel_and_publish")]
        public async Task CancelAndPublishAsync()
        {
            Logger.LogInformation("Image interaction canceled and published");
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            await Context.Interaction.UpdateAsync(x => x.Components = null).ConfigureAwait(false);
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
        }

        /// <summary>
        ///     Last page
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:last_page")]
        public async Task LastPageAsync()
        {
            var firstComponent = Context.Interaction.Message.Components?.FirstOrDefault()?.Components?.FirstOrDefault();
            if (firstComponent is not null)
            {
                var componentInteraction =
                    ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.CustomId == firstComponent.CustomId);
                if (componentInteraction.CustomId is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 - 1;
                    await FollowupAsync(components: ImageInteractions.GetOperationMenus(page).Build())
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Next page
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:next_page")]
        public async Task NextPageAsync()
        {
            var firstComponent = Context.Interaction.Message.Components?.FirstOrDefault()?.Components?.FirstOrDefault();
            if (firstComponent is not null)
            {
                var componentInteraction =
                    ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.CustomId == firstComponent.CustomId);
                if (componentInteraction.CustomId is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 + 1;
                    await FollowupAsync(components: ImageInteractions.GetOperationMenus(page).Build())
                        .ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}