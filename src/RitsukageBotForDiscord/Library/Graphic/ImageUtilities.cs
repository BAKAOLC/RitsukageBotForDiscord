using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic
{
    /// <summary>
    ///     Image utilities
    /// </summary>
    public static class ImageUtilities
    {
        /// <summary>
        ///     Fix GIF repeat count
        /// </summary>
        /// <param name="image">The image to fix</param>
        /// <typeparam name="T">The pixel type</typeparam>
        /// <returns></returns>
        public static Image<T> FixGifRepeatCount<T>(this Image<T> image) where T : unmanaged, IPixel<T>
        {
            var metadata = image.Metadata.GetGifMetadata();
            metadata.RepeatCount = 0;
            return image;
        }

        /// <summary>
        ///     Remove GIF global color table
        /// </summary>
        /// <param name="image"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Image<T> RemoveGifGlobalColorTable<T>(this Image<T> image) where T : unmanaged, IPixel<T>
        {
            image.Metadata.GetGifMetadata().GlobalColorTable = null;
            return image;
        }
    }
}