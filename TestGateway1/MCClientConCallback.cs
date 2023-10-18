using System;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Transactions;
using JTJabba.EasyConfig;
using MCGateway;
using MCGateway.Protocol;
using MCGateway.Protocol.V759;

namespace TestGateway1
{
    internal class MCClientConCallback : IMCClientConnectionCallback
    {
        bool _disposed;
        ILogger _logger = GatewayLogging.CreateLogger<MCClientConCallback>();
        IServerboundReceiver _serverBoundReceiver;
        public IMCServerConnection ServerConnection { get; private set; }

        MCClientConCallback(
            string username, Guid uuid, string? skin, IClientboundReceiver clientBoundReceiver, CancellationToken cancellationToken)
        {
            // Get TcpClient connected to backend server
            var serverClient = new TcpClient();
            serverClient.ConnectAsync("192.168.1.2", 25565, cancellationToken).AsTask().Wait(cancellationToken);
            serverClient.NoDelay = true;
            serverClient.ReceiveTimeout = Config.Timeouts.Backend.MCServerTimeout;
            serverClient.SendTimeout = Config.Timeouts.Backend.MCServerTimeout;
            serverClient.ReceiveBufferSize = Config.BufferSizes.ClientBound;
            serverClient.SendBufferSize = Config.BufferSizes.ServerBound;

            // Wrap connection and initiate
            var serverConnection = MCServerConnection.GetLoggedInConnection(serverClient, username, uuid, GetTranslationsObject(), clientBoundReceiver);

            if (ServerConnection is null) throw new Exception("Failed to get logged-in server connection");

            _serverBoundReceiver = serverConnection!;
            ServerConnection = serverConnection!;
        }
        public static Task<object> GetCallback(
            string username, Guid uuid, string? skin, IClientboundReceiver clientBoundReceiver, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return (object)new MCClientConCallback(username, uuid, skin, clientBoundReceiver, cancellationToken);
            });
        }

        public Config.TranslationsObject GetTranslationsObject()
        {
            return Translation.DefaultTranslation;
        }

        public void Forward(Packet packet)
        {
            _serverBoundReceiver.Forward(packet);
        }

        public Task SetSkin(string skin)
        {
            // Ignore for now ig
            return Task.CompletedTask;
        }

        public Task SetSkin(string skin, CancellationToken cancellationToken)
        {
            // Ignore for now ig
            return Task.CompletedTask;
        }

        public void StartedReceivingCallback()
        {
            ServerConnection.ReceiveTilClosedAndDispose();
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
