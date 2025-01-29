namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Host cancellation token
    /// </summary>
    public static class HostCancellationToken
    {
        private static readonly CancellationTokenSource CancellationTokenSource = new();

        /// <summary>
        ///     Token
        /// </summary>
        public static CancellationToken Token => CancellationTokenSource.Token;

        /// <summary>
        ///     Cancel the token
        /// </summary>
        public static void Cancel()
        {
            CancellationTokenSource.Cancel();
        }
    }
}