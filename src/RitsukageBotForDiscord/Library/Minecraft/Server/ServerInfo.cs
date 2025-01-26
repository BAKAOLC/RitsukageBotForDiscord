using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            return Task.Run(async () =>
            {
                TcpClient? tcp = null;

                if (!await TryResolveSrvRecordAsync().ConfigureAwait(false)) return;

                if (!TryOpenConnection(ref tcp)) return;

                var handler = new ProtocolHandler(tcp);

                try
                {
                    SendStatusRequest(tcp);
                    var result = ReadStatusResponse(handler);
                    SetInfoFromJsonText(result);
                    GetPing(tcp, handler);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Failed to get server info from {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                    State = ConnectionState.BadResponse;
                    Ping = -1;
                }

                tcp.Close();
                tcp.Dispose();
            });
        }

        private async Task<bool> TryResolveSrvRecordAsync()
        {
            try
            {
                logger?.LogDebug("Trying to resolve SRV record for {ServerAddress}", ServerAddress);
                var client = new LookupClient();
                var queryResponse = await client.QueryAsync("_minecraft._tcp." + ServerAddress, QueryType.SRV).ConfigureAwait(false);
                var result = queryResponse.Answers.OfType<SrvRecord>().FirstOrDefault();
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
                return true;
            }
            catch (DnsResponseException)
            {
                logger?.LogDebug("Failed to resolve SRV record for {ServerAddress}", ServerAddress);
                State = ConnectionState.BadConnect;
            }
            return false;
        }

        private bool TryOpenConnection([NotNullWhen(true)] ref TcpClient? tcp)
        {
            try
            {
                logger?.LogDebug("Trying to connect to {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                tcp = new(ServerAddress, ServerPort)
                {
                    ReceiveBufferSize = 1024 * 1024,
                    ReceiveTimeout = 30000,
                };
                return true;
            }
            catch (SocketException)
            {
                logger?.LogDebug("Failed to connect to {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
                State = ConnectionState.BadConnect;
            }
            return false;
        }

        private void SendStatusRequest(TcpClient tcp)
        {
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
        }

        private string ReadStatusResponse(ProtocolHandler handler)
        {
            logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
            var packetLength = handler.ReadNextVarIntRaw();
            if (packetLength == 0)
                throw new("Received empty packet from {ServerAddress}:{ServerPort}");

            var packetData = new List<byte>(handler.ReadDataRaw(packetLength));
            if (ProtocolHandler.ReadNextVarInt(packetData) != 0x00) throw new("Received unexpected packet ID from {ServerAddress}:{ServerPort}");

            logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress, ServerPort);
            return ProtocolHandler.ReadNextString(packetData);
        }

        private void GetPing(TcpClient tcp, ProtocolHandler handler)
        {
            var pingId = ProtocolHandler.GetVarInt(1);
            var pingContent = BitConverter.GetBytes((long)233);
            var pingPacket = ProtocolHandler.ConcatBytes(pingId, pingContent);
            var pingToSend = ProtocolHandler.ConcatBytes(ProtocolHandler.GetVarInt(pingPacket.Length), pingPacket);

            logger?.LogDebug("Sending ping packet to {ServerAddress}:{ServerPort}", ServerAddress,
                ServerPort);
            var pingWatcher = new Stopwatch();
            pingWatcher.Start();
            tcp.Client.Send(pingToSend, SocketFlags.None);

            var packetLength = handler.ReadNextVarIntRaw();
            pingWatcher.Stop();
            List<byte> packetData = [..handler.ReadDataRaw(packetLength)];
            if (packetData.Count <= 0 && ProtocolHandler.ReadNextVarInt(packetData) != 0x01)
                throw new("Received unexpected packet ID from {ServerAddress}:{ServerPort}");

            logger?.LogDebug("Reading response from {ServerAddress}:{ServerPort}", ServerAddress,
                ServerPort);
            long content = ProtocolHandler.ReadNextByte(packetData);
            if (content == 233) Ping = pingWatcher.ElapsedMilliseconds;
        }

        private void SetInfoFromJsonText(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText) || !jsonText.StartsWith('{') || !jsonText.EndsWith('}')) return;
            var jsonData = JObject.Parse(jsonText);

            TryGetVersion(jsonData);
            TryGetPlayers(jsonData);
            TryGetDescription(jsonData);
            TryGetIconData(jsonData);

            State = ConnectionState.Good;
        }

        private void TryGetVersion(JObject jsonData)
        {
            if (!jsonData.TryGetValue("version", out var versionDataToken)) return;
            var versionData = (JObject)versionDataToken;
            if (versionData.TryGetValue("name", out var nameDataToken)) GameVersion = nameDataToken.ToString();
            if (versionData.TryGetValue("protocol", out var protocolDataToken) &&
                int.TryParse(protocolDataToken.ToString(), out var protocol))
                ProtocolVersion = protocol;
        }

        private void TryGetPlayers(JObject jsonData)
        {
            if (!jsonData.TryGetValue("players", out var playersDataToken)) return;
            var playersData = (JObject)playersDataToken;
            if (playersData.TryGetValue("max", out var maxDataToken) && int.TryParse(maxDataToken.ToString(), out var max))
                MaxPlayerCount = max;

            if (playersData.TryGetValue("online", out var onlineDataToken) &&
                int.TryParse(onlineDataToken.ToString(), out var online))
                CurrentPlayerCount = online;

            if (!playersData.TryGetValue("sample", out var sampleDataToken) || sampleDataToken.Type is not JTokenType.Array)
                return;
            var result = new List<string>();
            foreach (var nameDataToken in sampleDataToken)
            {
                if (nameDataToken.Type is not JTokenType.Object) continue;
                var nameData = (JObject)nameDataToken;
                if (nameData.TryGetValue("name", out var name)) result.Add(name.ToString());
            }

            OnlinePlayersNames = [..result];
        }

        private void TryGetDescription(JObject jsonData)
        {
            if (!jsonData.TryGetValue("description", out var descriptionDataToken)) return;
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
        }

        private void TryGetIconData(JObject jsonData)
        {
            if (!jsonData.TryGetValue("favicon", out var faviconDataToken)) return;
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
        }
    }
}