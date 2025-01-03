using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     Invert color
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public class HalfMirror<T>(HalfMirror<T>.MirrorType type) : IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Mirror type
        /// </summary>
        public enum MirrorType
        {
            /// <summary>
            ///     Left
            /// </summary>
            Left,

            /// <summary>
            ///     Right
            /// </summary>
            Right,

            /// <summary>
            ///     Top
            /// </summary>
            Top,

            /// <summary>
            ///     Bottom
            /// </summary>
            Bottom,
        }

        /// <summary>
        ///     Mirror type
        /// </summary>
        public MirrorType Type { get; init; } = type;

        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        public Task ProcessAsync(ref Image<T>[] images)
        {
            foreach (var image in images) Process(image, Type);
            return Task.CompletedTask;
        }

        private static void Process(Image<T> image, MirrorType type)
        {
            switch (type)
            {
                case MirrorType.Left:
                {
                    var maxX = image.Width / 2;
                    for (var x = 0; x < maxX; x++)
                    for (var y = 0; y < image.Height; y++)
                        image[image.Width - x - 1, y] = image[x, y];
                    break;
                }
                case MirrorType.Right:
                {
                    var maxX = image.Width / 2;
                    for (var x = 0; x < maxX; x++)
                    for (var y = 0; y < image.Height; y++)
                        image[x, y] = image[image.Width - x - 1, y];
                    break;
                }
                case MirrorType.Top:
                {
                    var maxY = image.Height / 2;
                    for (var y = 0; y < maxY; y++)
                    for (var x = 0; x < image.Width; x++)
                        image[x, image.Height - y - 1] = image[x, y];
                    break;
                }
                case MirrorType.Bottom:
                {
                    var maxY = image.Height / 2;
                    for (var y = 0; y < maxY; y++)
                    for (var x = 0; x < image.Width; x++)
                        image[x, y] = image[x, image.Height - y - 1];
                    break;
                }
            }
        }
    }
}