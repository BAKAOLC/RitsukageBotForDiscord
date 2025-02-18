using RitsukageBot.Library.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic.Processing
{
    /// <summary>
    ///     HaHa Mirror
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public class DistortingMirror<T>(int cycleFrame = 30) : IProcessStep<T> where T : unmanaged, IPixel<T>
    {
        /// <summary>
        ///     Cycle Frame
        /// </summary>
        public int CycleFrame { get; set; } = cycleFrame;

        /// <summary>
        ///     Process
        /// </summary>
        /// <param name="images">The images to process</param>
        /// <returns></returns>
        public Task ProcessAsync(ref Image<T>[] images)
        {
            var originalFrames = images;
            var lcm = images.Length * CycleFrame / MathUtility.Gcd(images.Length, CycleFrame);
            var newImages = images = new Image<T>[lcm];
            var tasks = new Task[lcm];
            for (var i = 0; i < lcm; i++)
            {
                var index = i;
                var img = originalFrames[i % originalFrames.Length];
                var rate = (float)(i % CycleFrame) / CycleFrame;
                rate = rate < 0.5f ? rate * 2 : 1 - (rate - 0.5f) * 2;
                tasks[i] = Task.Run(() => newImages[index] = Process(img, rate));
            }

            return Task.WhenAll(tasks).ContinueWith(x =>
            {
                foreach (var image in originalFrames)
                    image.Dispose();
            });
        }

        private static Image<T> Process(Image<T> image, float rate)
        {
            var result = new Image<T>(image.Width, image.Height);
            var min = Math.Min(image.Width, image.Height);
            var center = new PointF(image.Width / 2f, image.Height / 2f);
            var half = min / 2f;
            for (var y = 0; y < result.Height; y++)
            for (var x = 0; x < result.Width; x++)
            {
                var dx = x - center.X;
                var dy = y - center.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance >= half)
                {
                    var angle = Math.Atan2(dy, dx);
                    var newDistance = distance * (1 + rate);
                    var newX = (int)(center.X + newDistance * Math.Cos(angle));
                    var newY = (int)(center.Y + newDistance * Math.Sin(angle));
                    if (newX >= 0 && newX < image.Width && newY >= 0 && newY < image.Height)
                        result[x, y] = image[newX, newY];
                }
                else
                {
                    var angle = Math.Atan2(dy, dx);
                    var newDistance = distance * (1 - rate);
                    var newX = (int)(center.X + newDistance * Math.Cos(angle));
                    var newY = (int)(center.Y + newDistance * Math.Sin(angle));
                    if (newX >= 0 && newX < image.Width && newY >= 0 && newY < image.Height)
                        result[x, y] = image[newX, newY];
                }
            }

            return result;
        }
    }
}