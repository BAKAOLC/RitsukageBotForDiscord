using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     Process step
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public interface IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        Task ProcessAsync(ref Image<T>[] images);
    }
}