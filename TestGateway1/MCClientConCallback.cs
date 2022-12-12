using System.Net.Sockets;
using System.Runtime.Versioning;
using JTJabba.EasyConfig;
using MCGateway;
using MCGateway.Protocol;
using MCGateway.Protocol.V759;

namespace TestGateway1
{
    internal class MCClientConCallback : IMCClientConnectionCallback
    {
        private bool _disposed;
        private ILogger _logger = GatewayLogging.CreateLogger<MCClientConCallback>();
        private IServerBoundReceiver _receiver;
        public IMCServerConnection ServerConnection { get; private set; }

        public MCClientConCallback(string username, Guid uuid, Config.TranslationsObject translation, IClientBoundReceiver selfReceiver)
        {
            var serverClient = new TcpClient("192.168.0.72", 25565);
            serverClient.NoDelay = true;
            serverClient.ReceiveTimeout = Config.KeepAlive.ServerTimeoutMs;
            serverClient.SendTimeout = Config.KeepAlive.ServerTimeoutMs;
            serverClient.ReceiveBufferSize = Config.BufferSizes.ClientBound;
            serverClient.SendBufferSize = Config.BufferSizes.ServerBound;
            var serverConnection = new MCServerConnection(serverClient, username, uuid, translation, selfReceiver);
            _receiver = serverConnection;
            ServerConnection = serverConnection;
        }
        [RequiresPreviewFeatures]
        public static IMCClientConnectionCallback GetCallback(string username, Guid uuid, Config.TranslationsObject translation, IClientBoundReceiver selfReceiver)
        {
            return new MCClientConCallback(username, uuid, translation, selfReceiver);
        }

        [RequiresPreviewFeatures]
        public static Config.TranslationsObject GetTranslationsObject(Guid playerGuid)
        {
            return Translation.DefaultTranslation;
        }

        public void Forward(Packet packet)
        {
            _receiver.Forward(packet);
        }

        public void SetSkin(string skin)
        {
            // Ignore for now ig
        }

        public void StartedReceivingCallback()
        {
            ServerConnection.ReceiveTilClosed();
        }


        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                ServerConnection.Dispose();
            }
            _disposed = true;
        }
    }
}
