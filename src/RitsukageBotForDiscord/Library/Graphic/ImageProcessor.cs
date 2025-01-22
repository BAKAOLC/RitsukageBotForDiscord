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
        private readonly List<IProcessStep<T>> _steps = [];
        private bool _disposed;
        private Image<T>[] _images;

        /// <summary>
        ///     Image processor
        /// </summary>
        public ImageProcessor(Image<T> image)
        {
            _images = new Image<T>[image.Frames.Count];
            for (var i = 0; i < image.Frames.Count; i++) _images[i] = image.Frames.CloneFrame(i);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Finalizer
        /// </summary>
        ~ImageProcessor()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Add process step
        /// </summary>
        /// <param name="step">What to do</param>
        /// <returns></returns>
        public ImageProcessor<T> AddProcessStep(IProcessStep<T> step)
        {
            _steps.Add(step);
            return this;
        }

        /// <summary>
        ///     Apply steps
        /// </summary>
        /// <returns>Image after processing</returns>
        public async Task<Image<T>> ProcessAsync()
        {
            foreach (var step in _steps) await step.ProcessAsync(ref _images).ConfigureAwait(false);

            var image = _images.First().Clone();
            image.RemoveGifGlobalColorTable();
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
        private ValueTask DisposeAsyncCore()
        {
            if (_disposed) return ValueTask.CompletedTask;
            foreach (var image in _images) image.Dispose();
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}