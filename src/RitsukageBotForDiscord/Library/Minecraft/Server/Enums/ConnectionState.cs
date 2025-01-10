namespace RitsukageBot.Library.Minecraft.Server.Enums
{
    /// <summary>
    ///     Represents the connection state of a server.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        ///     The server is online and responding.
        /// </summary>
        Good,

        /// <summary>
        ///     The server is online but not responding.
        /// </summary>
        BadResponse,

        /// <summary>
        ///     The server is offline.
        /// </summary>
        BadConnect,

        /// <summary>
        ///     The server is offline due to an exception.
        /// </summary>
        Exception,
    }
}