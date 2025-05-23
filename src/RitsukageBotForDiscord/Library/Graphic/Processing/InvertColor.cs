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
        public Task ProcessAsync(ref Image<T>[] images)
        {
            foreach (var image in images) Process(image);
            return Task.CompletedTask;
        }

        private static void Process(Image<T> image)
        {
            image.Mutate(x => x.Invert());
        }
    }
}