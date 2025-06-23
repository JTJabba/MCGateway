using MCGateway;
using MCGateway.DataTypes;
using MCGateway.Protocol;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.InterceptionServices;
using System.Net.Sockets;

namespace PingPongDemo
{
    internal sealed class GatewayConCallback : IGatewayConnectionCallback
    {
        IMCClientConCallbackFactory _clientCallbackFactory;
        BidirectionalDictionary<string, Guid> _onlinePlayers = new();
        ServiceManager _serviceManager;
        public bool InOfflineMode => false;

        public GatewayConCallback(IMCClientConCallbackFactory clientCallbackFactory, ServiceManager serviceManager)
        {
            _clientCallbackFactory = clientCallbackFactory;
            _serviceManager = serviceManager;
        }

        public ReadOnlySpan<byte> GetStatusResponse(Handshake handshake)
        {
            byte[] iconBytes = File.ReadAllBytes("gateway.png");
            string icon = Convert.ToBase64String(iconBytes);
            var statusString = IGatewayConnectionCallback.GetStatusResponseString(0, 0, Array.Empty<Tuple<string, string?>>(),
                "Test gateway server but by Mo ", icon, "1.19", 759);

            var bytes = IGatewayConnectionCallback.GetStatusResponseBytes(statusString);
            return new ReadOnlySpan<byte>(bytes.buffer, 0, bytes.bytesWritten);
        }

        public bool TryAddOnlinePlayer(string username, Guid uuid)
        {
            _serviceManager.AlertServicesOfPlayerJoin(uuid, username);
            return _onlinePlayers.TryAdd(username, uuid);
        }

        public void RemoveOnlinePlayer(Guid uuid)
        {
            _serviceManager.AlertServicesOfPlayerLeave(uuid, _onlinePlayers.Inverse[uuid]);
            _onlinePlayers.Inverse.Remove(uuid);
        }

        public IMCClientConnection? GetLoggedInClientConnection(Handshake handshake, TcpClient tcpClient)
        {
            if (handshake.ProtocolVersion != 759) return null; // TODO add support for disconnecting player with message
            return MCClientConnection.GetLoggedInClientConnection(
                tcpClient, _clientCallbackFactory,  TryAddOnlinePlayer, RemoveOnlinePlayer);
        }
    }
}
