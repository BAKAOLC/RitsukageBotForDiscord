using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Graphic;
using RitsukageBot.Library.Graphic.Processing;
using RitsukageBot.Services.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Modules.Interactions
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
        ];

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
            await Context.Interaction.DeferAsync(true);
            Image<Rgba32>? image = null;
            var message = string.Empty;
            var success = false;
            try
            {
                image = await ImageCacheProviderService.GetImageAsync(url);
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
                await ModifyOriginalResponseAsync(x => { x.Content = string.IsNullOrEmpty(message) ? "Failed to download image" : message; });
                return;
            }

            var imageStream = new MemoryStream();
            string fileName;
            if (image.Frames.Count > 1)
            {
                await image.SaveAsGifAsync(imageStream);
                fileName = "image.gif";
            }
            else
            {
                await image.SaveAsPngAsync(imageStream);
                fileName = "image.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Attachments = new List<FileAttachment>
                {
                    new(imageStream, fileName),
                };
                x.Components = GetOperationMenus().Build();
            });
        }

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
                    for (var i = 0; i < 4; i++)
                    {
                        row1.WithButton(interactions[i].AsButtonBuilder());
                    }

                    var row2 = new ActionRowBuilder();
                    for (var i = 4; i < interactions.Length; i++)
                    {
                        row2.WithButton(interactions[i].AsButtonBuilder());
                    }

                    builder.AddRow(row1);
                    builder.AddRow(row2);
                    break;
                }
                case > 0:
                {
                    var row = new ActionRowBuilder();
                    foreach (var interaction in interactions)
                    {
                        row.WithButton(interaction.AsButtonBuilder());
                    }

                    builder.AddRow(row);
                    break;
                }
            }

            var operationRow = new ActionRowBuilder();
            if (hasLastPage)
            {
                operationRow.WithButton(LastPageInteraction.AsButtonBuilder());
            }

            if (hasNextPage)
            {
                operationRow.WithButton(NextPageInteraction.AsButtonBuilder());
            }

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
        public readonly record struct AllowedInteraction(string Label, string CustomId, ButtonStyle? ButtonStyle = null, IEmote? Emote = null)
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

                if (Emote is not null)
                {
                    builder.WithEmote(Emote);
                }

                builder.WithDisabled(disabled);
                return builder;
            }
        }
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
        ///     Cancel image interaction
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:cancel")]
        public Task CancelAsync()
        {
            Logger.LogInformation("Image interaction canceled");
            return Context.Interaction.UpdateAsync(x => x.Components = null);
        }

        /// <summary>
        ///     Cancel and publish image interaction
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:cancel_and_publish")]
        public async Task CancelAndPublishAsync()
        {
            Logger.LogInformation("Image interaction canceled and published");
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            await Context.Interaction.UpdateAsync(x => x.Components = null);
            if (attachment is not null) await Context.Channel.SendMessageAsync(attachment.Url);
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
                var componentInteraction = ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.CustomId == firstComponent.CustomId);
                if (componentInteraction.CustomId is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 - 1;
                    await Context.Interaction.UpdateAsync(x => x.Components = ImageInteractions.GetOperationMenus(page).Build());
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
                var componentInteraction = ImageInteractions.AllowedInteractions.FirstOrDefault(x => x.CustomId == firstComponent.CustomId);
                if (componentInteraction.CustomId is not null)
                {
                    var index = Array.IndexOf(ImageInteractions.AllowedInteractions, componentInteraction);
                    var page = index / 8 + 1;
                    await Context.Interaction.UpdateAsync(x => x.Components = ImageInteractions.GetOperationMenus(page).Build());
                }
            }
        }

        /// <summary>
        ///     Invert color
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:invert_color")]
        public async Task InvertColorAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new InvertColor<Rgba32>());
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        /// <summary>
        ///     Invert frames
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:invert_frames")]
        public async Task InvertFrameAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new InvertFrames<Rgba32>());
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        /// <summary>
        ///     Mirror left
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_left")]
        public async Task MirrorLeftAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new HalfMirror<Rgba32>(HalfMirror<Rgba32>.MirrorType.Left));
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        /// <summary>
        ///     Mirror right
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_right")]
        public async Task MirrorRightAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new HalfMirror<Rgba32>(HalfMirror<Rgba32>.MirrorType.Right));
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        /// <summary>
        ///     Mirror top
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_top")]
        public async Task MirrorTopAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new HalfMirror<Rgba32>(HalfMirror<Rgba32>.MirrorType.Top));
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        /// <summary>
        ///     Mirror bottom
        /// </summary>
        [ComponentInteraction($"{ImageInteractions.TagCustomId}:mirror_bottom")]
        public async Task MirrorBottomAsync()
        {
            var image = await GetImage();
            if (image is null)
            {
                return;
            }

            var processor = new ImageProcessor<Rgba32>(image);
            processor.AddProcessStep(new HalfMirror<Rgba32>(HalfMirror<Rgba32>.MirrorType.Bottom));
            var result = await processor.ProcessAsync();
            await SetImage(result);
        }

        private async Task<Image<Rgba32>?> GetImage()
        {
            var attachment = Context.Interaction.Message.Attachments.FirstOrDefault();
            if (attachment is null)
            {
                return null;
            }

            Image<Rgba32>? image = null;
            try
            {
                image = await ImageCacheProviderService.GetImageAsync(attachment.Url);
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
                await image.SaveAsGifAsync(imageStream);
                fileName = "image.gif";
            }
            else
            {
                await image.SaveAsPngAsync(imageStream);
                fileName = "image.png";
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            image.Dispose();

            await Context.Interaction.UpdateAsync(x =>
            {
                x.Attachments = new List<FileAttachment>
                {
                    new(imageStream, fileName),
                };
            });
        }
    }
}