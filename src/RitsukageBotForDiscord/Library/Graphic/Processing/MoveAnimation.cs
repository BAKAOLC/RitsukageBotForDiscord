using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     Move Animation
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public class MoveAnimation<T>(MoveAnimation<T>.MoveDirection direction)
        : IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Mirror type
        /// </summary>
        public enum MoveDirection
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
            ///     Up
            /// </summary>
            Up,

            /// <summary>
            ///     Down
            /// </summary>
            Down,
        }

        /// <summary>
        ///     Move Direction
        /// </summary>
        public MoveDirection Direction { get; init; } = direction;

        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        public Task ProcessAsync(ref Image<T>[] images)
        {
            var length = images.Length;
            var tasks = new Task[length];
            for (var i = 0; i < length; i++)
            {
                var image = images[i];
                var index = i;
                tasks[i] = Task.Run(() => Process(image, Direction, index, length));
            }

            return Task.WhenAll(tasks);
        }

        private static void Process(Image<T> image, MoveDirection direction, int index, int total)
        {
            var original = image.Clone();
            var width = image.Width;
            var height = image.Height;
            switch (direction)
            {
                case MoveDirection.Left:
                {
                    var offset = width * index / total;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var targetX = x + offset;
                        if (targetX >= width)
                            targetX -= width;
                        image[x, y] = original[targetX, y];
                    }

                    break;
                }
                case MoveDirection.Right:
                {
                    var offset = width * index / total;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var targetX = x - offset;
                        if (targetX < 0)
                            targetX += width;
                        image[x, y] = original[targetX, y];
                    }

                    break;
                }
                case MoveDirection.Up:
                {
                    var offset = height * index / total;
                    for (var y = 0; y < height; y++)
                    {
                        var targetY = y + offset;
                        if (targetY >= height)
                            targetY -= height;
                        for (var x = 0; x < width; x++)
                            image[x, y] = original[x, targetY];
                    }

                    break;
                }
                case MoveDirection.Down:
                {
                    var offset = height * index / total;
                    for (var y = 0; y < height; y++)
                    {
                        var targetY = y - offset;
                        if (targetY < 0)
                            targetY += height;
                        for (var x = 0; x < width; x++)
                            image[x, y] = original[x, targetY];
                    }

                    break;
                }
            }
        }
    }
}