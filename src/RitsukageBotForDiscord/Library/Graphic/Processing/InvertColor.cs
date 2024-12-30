using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     Invert color
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public class InvertColor<T> : IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        public Task ProcessAsync(Image<T>[] images)
        {
            foreach (var image in images) image.Mutate(x => x.Invert());

            return Task.CompletedTask;
        }
    }
}