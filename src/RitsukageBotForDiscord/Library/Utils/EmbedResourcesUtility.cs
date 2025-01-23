using System.Reflection;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Utility for embedded resources.
    /// </summary>
    public static class EmbedResourcesUtility
    {
        /// <summary>
        ///     Gets the stream of the embedded resource.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static Stream GetStream(Uri uri, Assembly? assembly = null)
        {
            if (uri.Scheme != "embedded")
                throw new NotSupportedException("Scheme not supported");

            assembly ??= Assembly.GetExecutingAssembly();
            var defaultNamespace = assembly.GetName().Name;
            var resourcePath = uri.Host + uri.AbsolutePath;
            if (!resourcePath.StartsWith('/'))
                resourcePath = '/' + resourcePath;
            resourcePath = $"{defaultNamespace}{resourcePath.Replace('\\', '.').Replace('/', '.')}";
            using var stream = assembly.GetManifestResourceStream(resourcePath) ??
                               throw new InvalidOperationException($"Resource not found: {resourcePath}");
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
    }
}