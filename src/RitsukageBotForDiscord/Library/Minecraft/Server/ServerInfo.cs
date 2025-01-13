using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Minecraft.Server.Enums;

namespace RitsukageBot.Library.Minecraft.Server
{
    /// <summary>
    ///     Minecraft Server Info
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <param name="logger"></param>
    public class ServerInfo(string ip, ushort port, ILogger? logger = null)
    {
        /// <summary>
        ///     Server Address
        /// </summary>
        public string ServerAddress { get; private set; } = ip;

        /// <summary>
        ///     Server Port
        /// </summary>
        public ushort ServerPort { get; private set; } = port;

        /// <summary>
        ///     Server Motd
        /// </summary>
        public string Motd { get; private set; } = string.Empty;

        /// <summary>
        ///     Max Player Count
        /// </summary>
        public int MaxPlayerCount { get; private set; }

        /// <summary>
        ///     Current Player Count
        /// </summary>
        public int CurrentPlayerCount { get; private set; }

        /// <summary>
        ///     Protocol Version
        /// </summary>
        public int ProtocolVersion { get; private set; }

        /// <summary>
        ///     Game Version
        /// </summary>
        public string GameVersion { get; private set; } = string.Empty;

        /// <summary>
        ///     Online Players Name
        /// </summary>
        public string[] OnlinePlayersNames { get; private set; } = [];

        /// <summary>
        ///     Ping
        /// </summary>
        public long Ping { get; private set; }

        /// <summary>
        ///     Icon Data
        /// </summary>
        public byte[] IconData { get; private set; } = [];

        /// <summary>
        ///     Connection State
        /// </summary>
        public ConnectionState State { get; private set; }

        /// <summary>
        ///     Get Server Info
        /// </summary>
        public Task StartGetServerInfoAsync()
        {
            return Task.Run(() =>
            {
                TcpClient tcp;

                try
                {
                    logger?.LogDebug("Trying to resolve SRV record for {ServerAddress}", ServerAddress);
                    var client = new LookupClient();
                    var result = client.Query("_minecraft._tcp." + ServerAddress, QueryType.SRV).Answers
                        .OfType<SrvRecord>().FirstOrDefault();
                    if (result == null)
                    {
                        logger?.LogDebug("Failed to resolve SRV record for {ServerAddress}", ServerAddress);
                    }
                    else
                    {
                        var target = result.Target.Value.TrimEnd('.');
                        logger?.LogDebug("Resolved SRV record for {ServerAddress} to {Target}:{Port}", ServerAddress,
                            target, result.Port);
                        ServerAddress = target;
                        ServerPort = result.Port;
                    }
                }
                catch (DnsResponseException)
                {
                    logger?.LogDebug("Failed to resolve SRV record for {ServerAddress}", ServerAddress);
                    State = ConnectionState.BadConnect;
                    return;
                }

                try
                {
                    logger?.LogDebug("Trying to connect to {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    tcp = new(ServerAddress, ServerPort);
                }
                catch (SocketException)
                {
                    logger?.LogDebug("Failed to connect to {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    State = ConnectionState.BadConnect;
                    return;
                }

                try
                {
                    tcp.ReceiveBufferSize = 1024 * 1024;
                    tcp.ReceiveTimeout = 30000;

                    var packetId = ProtocolHandler.GetVarInt(0);
                    var protocolVersion = ProtocolHandler.GetVarInt(-1);
                    var serverAddressVal = Encoding.UTF8.GetBytes(ServerAddress);
                    var serverAddressLen = ProtocolHandler.GetVarInt(serverAddressVal.Length);
                    var serverPort = BitConverter.GetBytes(ServerPort);
                    Array.Reverse(serverPort);

                    var nextState = ProtocolHandler.GetVarInt(1);
                    var packet2 = ProtocolHandler.ConcatBytes(packetId, protocolVersion, serverAddressLen,
                        serverAddressVal, serverPort, nextState);
                    var toSend = ProtocolHandler.ConcatBytes(ProtocolHandler.GetVarInt(packet2.Length), packet2);
                    var statusRequest = ProtocolHandler.GetVarInt(0);
                    var requestPacket = ProtocolHandler.ConcatBytes(ProtocolHandler.GetVarInt(statusRequest.Length),
                        statusRequest);

                    logger?.LogDebug("Sending handshake packet to {ServerAddress}:{ServerPort}", ServerAddress,
                        ServerPort);
                    tcp.Client.Send(toSend, SocketFlags.None);

                    logger?.LogDebug("Sending status request packet to {ServerAddress}:{ServerPort}", ServerAddress,
                        ServerPort);
                    tcp.Client.Send(requestPacket, SocketFlags.None);

                    logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    var handler = new ProtocolHandler(tcp);
                    var packetLength = handler.ReadNextVarIntRaw();
                    if (packetLength == 0)
                    {
                        logger?.LogDebug("Received empty packet from {ServerAddress}:{ServerPort}", ServerAddress,
                            ServerPort);
                        State = ConnectionState.BadResponse;
                        return;
                    }

                    var packetData = new List<byte>(handler.ReadDataRaw(packetLength));
                    if (ProtocolHandler.ReadNextVarInt(packetData) != 0x00)
                    {
                        logger?.LogDebug("Received unexpected packet ID from {ServerAddress}:{ServerPort}",
                            ServerAddress, ServerPort);
                        State = ConnectionState.BadResponse;
                        return;
                    }

                    logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    var result = ProtocolHandler.ReadNextString(packetData);
                    SetInfoFromJsonText(result);

                    var pingId = ProtocolHandler.GetVarInt(1);
                    var pingContent = BitConverter.GetBytes((long)233);
                    var pingPacket = ProtocolHandler.ConcatBytes(pingId, pingContent);
                    var pingToSend =
                        ProtocolHandler.ConcatBytes(ProtocolHandler.GetVarInt(pingPacket.Length), pingPacket);

                    try
                    {
                        logger?.LogDebug("Sending ping packet to {ServerAddress}:{ServerPort}", ServerAddress,
                            ServerPort);
                        var pingWatcher = new Stopwatch();
                        pingWatcher.Start();
                        tcp.Client.Send(pingToSend, SocketFlags.None);

                        packetLength = handler.ReadNextVarIntRaw();
                        pingWatcher.Stop();
                        if (packetLength > 0)
                        {
                            packetData = [..handler.ReadDataRaw(packetLength)];
                            if (ProtocolHandler.ReadNextVarInt(packetData) != 0x01)
                            {
                                logger?.LogDebug("Received unexpected packet ID from {ServerAddress}:{ServerPort}",
                                    ServerAddress, ServerPort);
                                State = ConnectionState.BadResponse;
                                return;
                            }
                        }

                        logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress,
                            ServerPort);
                        long content = ProtocolHandler.ReadNextByte(packetData);
                        if (content == 233) Ping = pingWatcher.ElapsedMilliseconds;
                    }
                    catch (Exception)
                    {
                        logger?.LogDebug("Failed to send ping packet to {ServerAddress}:{ServerPort}", ServerAddress,
                            ServerPort);
                        Ping = -1;
                    }
                }
                catch (SocketException)
                {
                    logger?.LogDebug("Failed to connect to {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    State = ConnectionState.BadResponse;
                }

                tcp.Close();
                tcp.Dispose();
            });
        }

        private void SetInfoFromJsonText(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText) || !jsonText.StartsWith('{') || !jsonText.EndsWith('}')) return;
            var jsonData = JObject.Parse(jsonText);

            if (jsonData.TryGetValue("version", out var versionDataToken))
            {
                var versionData = (JObject)versionDataToken;
                if (versionData.TryGetValue("name", out var token)) GameVersion = token.ToString();

                if (versionData.TryGetValue("protocol", out token) && int.TryParse(token.ToString(), out var protocol))
                    ProtocolVersion = protocol;
            }

            if (jsonData.TryGetValue("players", out var playersDataToken))
            {
                var playerData = (JObject)playersDataToken;
                if (playerData.TryGetValue("max", out var token) && int.TryParse(token.ToString(), out var max))
                    MaxPlayerCount = max;

                if (playerData.TryGetValue("online", out token) && int.TryParse(token.ToString(), out var online))
                    CurrentPlayerCount = online;

                if (playerData.TryGetValue("sample", out token) && token.Type == JTokenType.Array)
                {
                    var result = new List<string>();
                    foreach (var nameDataToken in token)
                    {
                        if (nameDataToken.Type is not JTokenType.Object) continue;
                        var nameData = (JObject)nameDataToken;
                        if (nameData.TryGetValue("name", out var name)) result.Add(name.ToString());
                    }

                    OnlinePlayersNames = [.. result];
                }
            }

            if (jsonData.TryGetValue("description", out var descriptionDataToken))
                switch (descriptionDataToken.Type)
                {
                    case JTokenType.String:
                        Motd = descriptionDataToken.ToString();
                        break;
                    case JTokenType.Object:
                    {
                        var descriptionData = (JObject)descriptionDataToken;
                        if (descriptionData.TryGetValue("extra", out var extraDataToken))
                            foreach (var itemToken in extraDataToken)
                            {
                                if (itemToken.Type is not JTokenType.Object) continue;
                                var itemData = (JObject)itemToken;
                                if (!itemData.TryGetValue("text", out var text)) continue;
                                var motd = text.ToString();
                                if (!string.IsNullOrEmpty(motd)) Motd += motd;
                            }
                        else if (descriptionData.TryGetValue("text", out var textDataToken))
                            Motd = textDataToken.ToString();

                        break;
                    }
                }

            if (jsonData.TryGetValue("favicon", out var faviconDataToken))
                try
                {
                    var faviconData = faviconDataToken.ToString();
                    var byteData = Convert.FromBase64String(faviconData.Replace("data:image/png;base64,", ""));
                    IconData = byteData;
                }
                catch
                {
                    IconData = [];
                }

            State = ConnectionState.Good;
        }
    }
}