using RitsukageBot.Library.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RitsukageBot.Library.Graphic.Generators
{
    /// <summary>
    ///     Generates good and bad news images.
    /// </summary>
    public static class GoodBadNewsGenerators
    {
        /// <summary>
        ///     Gets the good news background image.
        /// </summary>
        /// <returns></returns>
        public static Image<Rgba32> GetGoodNewsBackground()
        {
            return Image.Load<Rgba32>(EmbedResourcesUtility.GetStream(new("embedded://static/good_news.jpg")));
        }

        /// <summary>
        ///     Gets the bad news background image.
        /// </summary>
        /// <returns></returns>
        public static Image<Rgba32> GetBadNewsBackground()
        {
            return Image.Load<Rgba32>(EmbedResourcesUtility.GetStream(new("embedded://static/bad_news.jpg")));
        }

        /// <summary>
        ///     Gets the good news color.
        /// </summary>
        /// <returns></returns>
        public static Rgba32 GetGoodNewsColor()
        {
            return new(220, 48, 35);
        }

        /// <summary>
        ///     Gets the bad news color.
        /// </summary>
        /// <returns></returns>
        public static Rgba32 GetBadNewsColor()
        {
            return new(90, 90, 90);
        }


        /// <summary>
        ///     Generates a good news image.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Image<Rgba32> GenerateGoodNewsImage(string text)
        {
            var image = GetGoodNewsBackground();
            var color = GetGoodNewsColor();
            var font = FontUtility.GetDefaultFont(96, FontStyle.Bold);
            image.Mutate(x =>
            {
                x.DrawText(new(font)
                {
                    Origin = new(24, image.Height / 2f),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrappingLength = image.Width - 48,
                }, text, color);
            });
            return image;
        }

        /// <summary>
        ///     Generates a bad news image.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Image<Rgba32> GenerateBadNewsImage(string text)
        {
            var image = GetBadNewsBackground();
            var color = GetBadNewsColor();
            var font = FontUtility.GetDefaultFont(96, FontStyle.Bold);
            image.Mutate(x =>
            {
                x.DrawText(new(font)
                {
                    Origin = new(24, image.Height / 2f),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrappingLength = image.Width - 48,
                }, text, color);
            });
            return image;
        }
    }
}