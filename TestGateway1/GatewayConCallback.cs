using MCGateway;
using MCGateway.DataTypes;
using MCGateway.Protocol;
using System.Net.Sockets;

namespace TestGateway1
{
    public class GatewayConCallback : IGatewayConnectionCallback
    {
        static BidirectionalDictionary<string, Guid> OnlinePlayers = new();
        public bool InOfflineMode => false;

        public ReadOnlySpan<byte> GetStatusResponse(Handshake handshake)
        {
            byte[] iconBytes = File.ReadAllBytes("gateway.png");
            string icon = Convert.ToBase64String(iconBytes);
            var statusString = IGatewayConnectionCallback.GetStatusResponseString(0, 0, Array.Empty<Tuple<string, string?>>(),
                "Test gateway server", icon, "1.19", 759);

            var bytes = IGatewayConnectionCallback.GetStatusResponseBytes(statusString);
            return new ReadOnlySpan<byte>(bytes.buffer, 0, bytes.bytesWritten);
        }

        public bool TryAddOnlinePlayer(string username, Guid uuid)
        {
            return OnlinePlayers.TryAdd(username, uuid);
        }

        public void RemoveOnlinePlayer(Guid uuid)
        {
            OnlinePlayers.Inverse.Remove(uuid);
        }

        public IMCClientConnection? GetLoggedInClientConnection(Handshake handshake, TcpClient tcpClient)
        {
            if (handshake.ProtocolVersion != 759) return null; // TODO add support for disconnecting player with message
            return MCGateway.Protocol.V759.MCClientConnection<MCClientConCallback>
                .GetLoggedInClientConnection<MCClientConCallback>(tcpClient, TryAddOnlinePlayer, RemoveOnlinePlayer);
        }
    }
}
