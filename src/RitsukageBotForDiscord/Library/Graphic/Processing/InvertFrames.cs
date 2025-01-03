using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     Invert frames
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public class InvertFrames<T> : IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        public Task ProcessAsync(ref Image<T>[] images)
        {
            Array.Reverse(images);
            return Task.CompletedTask;
        }
    }
}