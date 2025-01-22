using System.Text;
using RitsukageBot.Library.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RitsukageBot.Library.Graphic.Generators
{
    /// <summary>
    ///     Converts an image to a new image with color characters.
    /// </summary>
    public static class ColorfulCharsImageConvertor
    {
        private static readonly char[] ColorChars = [' ', '.', ':', '-', '=', '+', '*', '#', '%', '@'];

        private static ColorChar GetColorChar(Rgba32 color)
        {
            var brightness = (color.R + color.G + color.B) / 3;
            return new(ColorChars[(int)(brightness / 255.0 * (ColorChars.Length - 1))], color);
        }

        private static ColorChar[] InnerConvertSingleFrame(Image<Rgba32> image)
        {
            var chars = new ColorChar[image.Width * image.Height];
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var color = image[x, y];
                chars[y * image.Width + x] = GetColorChar(color);
            }
            return chars;
        }

        private static Image<Rgba32> InnerBuildImage(ColorChar[] colorChars, Font font, int widthCharCount, out string charImage)
        {
            if (colorChars.Length % widthCharCount != 0)
                throw new ArgumentException("The widthCharCount is invalid.", nameof(widthCharCount));

            var outputImage = new Image<Rgba32>(widthCharCount * (int)font.Size, colorChars.Length / widthCharCount * (int)font.Size);
            var charImageBuilder = new StringBuilder();
            for (var y = 0; y < colorChars.Length / widthCharCount; y++)
            {
                if (y != 0)
                    charImageBuilder.AppendLine();
                for (var x = 0; x < widthCharCount; x++)
                {
                    var colorChar = colorChars[y * widthCharCount + x];
                    charImageBuilder.Append(colorChar.Char);
                    if (char.IsWhiteSpace(colorChar.Char)) continue;
                    var ix = x * (int)font.Size + (int)font.Size / 2;
                    var iy = y * (int)font.Size + (int)font.Size / 2;
                    outputImage.Mutate(ctx =>
                    {
                        ctx.DrawText(new(font)
                        {
                            Origin = new(ix, iy),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        }, colorChar.Char.ToString(), colorChar.Color);
                    });
                }
            }
            charImage = charImageBuilder.ToString();
            return outputImage;
        }

        /// <summary>
        ///     Converts an image to a new image with color characters.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="charSize"></param>
        /// <param name="pixelSize"></param>
        /// <param name="charImages"></param>
        /// <returns></returns>
        public static Image<Rgba32> Convert(Image<Rgba32> image, int charSize, int pixelSize, out string[] charImages)
        {
            using var sampleImage = image.Clone();
            sampleImage.Mutate(x => x.Resize(image.Width / pixelSize, image.Height / pixelSize));
            charImages = new string[sampleImage.Frames.Count];
            var resultImage = new Image<Rgba32>(sampleImage.Width * charSize, sampleImage.Height * charSize);
            var font = FontUtility.GetDefaultFont(charSize);
            for (var i = 0; i < sampleImage.Frames.Count; i++)
            {
                using var frameImage = sampleImage.Frames.CloneFrame(i);
                var chars = InnerConvertSingleFrame(frameImage);
                using var charImage = InnerBuildImage(chars, font, sampleImage.Width, out var charImageStr);
                var frameMetadata = charImage.Frames.RootFrame.Metadata.GetGifMetadata();
                frameMetadata.FrameDelay = image.Frames[i].Metadata.GetGifMetadata().FrameDelay;
                frameMetadata.DisposalMethod = GifDisposalMethod.RestoreToBackground;
                resultImage.Frames.AddFrame(charImage.Frames.RootFrame);
                charImages[i] = charImageStr;
            }
            resultImage.Frames.RemoveFrame(0);
            resultImage.Metadata.GetGifMetadata().RepeatCount = image.Metadata.GetGifMetadata().RepeatCount;
            return resultImage;
        }

        /// <summary>
        ///     Converts an image to a new image with color characters.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="pixelSize"></param>
        /// <returns></returns>
        public static string ConvertToString(Image<Rgba32> image, int pixelSize)
        {
            using var sampleImage = image.Frames.CloneFrame(0);
            sampleImage.Mutate(x => x.Resize(image.Width / pixelSize, image.Height / pixelSize));
            var chars = InnerConvertSingleFrame(sampleImage);
            var charImageStr = new StringBuilder();
            for (var y = 0; y < sampleImage.Height; y++)
            {
                if (y != 0)
                    charImageStr.AppendLine();
                for (var x = 0; x < sampleImage.Width; x++)
                {
                    var colorChar = chars[y * sampleImage.Width + x];
                    charImageStr.Append(colorChar.Char);
                }
            }
            return charImageStr.ToString();
        }

        /// <summary>
        ///     A record struct that contains a character and a color.
        /// </summary>
        /// <param name="Char"></param>
        /// <param name="Color"></param>
        public record struct ColorChar(char Char, Rgba32 Color);
    }
}