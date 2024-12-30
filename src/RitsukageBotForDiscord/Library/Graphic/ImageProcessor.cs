using RitsukageBot.Library.Graphic.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Library.Graphic
{
    /// <summary>
    ///     Image processor
    /// </summary>
    /// <typeparam name="T">pixel type</typeparam>
    public sealed class ImageProcessor<T> : IDisposable, IAsyncDisposable
        where T : unmanaged, IPixel<T>
    {
        private readonly Image<T>[] _images;
        private readonly List<IProcessStep<T>> _steps = [];
        private bool _disposed;

        /// <summary>
        ///     Image processor
        /// </summary>
        public ImageProcessor(Image<T> image)
        {
            _images = new Image<T>[image.Frames.Count];
            for (var i = 0; i < image.Frames.Count; i++) _images[i] = image.Frames.CloneFrame(i);
        }
        
        ~ImageProcessor()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Dispose async
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Add process step
        /// </summary>
        /// <param name="step">What to do</param>
        /// <returns></returns>
        public ImageProcessor<T> AddStep(IProcessStep<T> step)
        {
            _steps.Add(step);
            return this;
        }

        /// <summary>
        ///     Apply steps
        /// </summary>
        /// <returns>Image after processing</returns>
        public async Task<Image<T>> ApplyStepsAsync()
        {
            foreach (var step in _steps) await step.ProcessAsync(_images);

            var image = _images.First().Clone();
            for (var i = 1; i < _images.Length; i++) image.Frames.AddFrame(_images[i].Frames.RootFrame);
            return image;
        }

        /// <summary>
        ///     Dispose
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
                foreach (var image in _images)
                    image.Dispose();

            _disposed = true;
        }

        /// <summary>
        ///     Dispose async
        /// </summary>
        private async ValueTask DisposeAsyncCore()
        {
            if (_disposed) return;
            foreach (var image in _images) image.Dispose();
            await Task.CompletedTask;
            _disposed = true;
        }
    }
}