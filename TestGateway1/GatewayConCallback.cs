using System.Collections.Concurrent;
using MCGateway;
using MCGateway.Protocol;
using System.Net.Sockets;
using System.Runtime.Versioning;
using JTJabba.EasyConfig;

namespace TestGateway1
{
    internal class GatewayConCallback : IGatewayConnectionCallback
    {
        static BidirectionalDictionary<string, Guid> OnlinePlayers = new();
        public bool InOfflineMode => false;

        public static IGatewayConnectionCallback GetCallback((string serverAddress, ushort serverPort, int protocolVersion) handshake)
        {
            return new GatewayConCallback();
        }

        public static ReadOnlySpan<byte> GetStatusResponse((string ServerAddress, ushort ServerPort, int ProtocolVersion) handshake)
        {
            byte[] iconBytes = File.ReadAllBytes("gateway.png");
            string icon = Convert.ToBase64String(iconBytes);
            var statusString = IGatewayConnectionCallback.GetStatusResponseString(0, 0, Array.Empty<Tuple<string, string?>>(),
                "Test gateway server", icon, "1.19", 759);

            var bytes = IGatewayConnectionCallback.GetStatusResponseBytes(statusString);
            return new ReadOnlySpan<byte>(bytes.buffer, 0, bytes.bytesWritten);
        }

        public static bool TryAddOnlinePlayer(string username, Guid uuid)
        {
            return OnlinePlayers.TryAdd(username, uuid);
        }

        public static void RemoveOnlinePlayer(Guid uuid)
        {
            OnlinePlayers.Inverse.Remove(uuid);
        }

        public IMCClientConnection? GetLoggedInClientConnection(TcpClient tcpClient)
        {
            return MCGateway.Protocol.V759.MCClientConnection<MCClientConCallback>
                .GetLoggedInClientConnection<MCClientConCallback>(tcpClient, TryAddOnlinePlayer, RemoveOnlinePlayer);
        }
    }
}
