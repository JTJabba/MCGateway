using JTJabba.EasyConfig;
using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.InterceptionServices;
using System.Net.Sockets;

namespace PingPongDemo.MCClientConCallbackFactories
{
    internal sealed class MC4FactoryMain : IMCClientConCallbackFactory
    {
        private readonly MainFactoryServiceContainer _serviceProvider;
        private readonly ServiceManager _serviceManager;

        public MC4FactoryMain(ConnectionsDictionary connectionDict, ServiceManager serviceManager)
        {
            _serviceProvider = new MainFactoryServiceContainer(connectionDict);
            _serviceManager = serviceManager;
        }

        public Task<IMCClientConCallback> GetCallback(MCClientConnection clientConnection, CancellationToken cancellationToken)
        {
            // Get TcpClient connected to backend server
            var serverClient = new TcpClient();
            serverClient.ConnectAsync(DemoConfig.BackendServerAddress, DemoConfig.BackendServerPort, cancellationToken).AsTask().Wait(cancellationToken);
            serverClient.NoDelay = true;
            serverClient.ReceiveTimeout = Config.Timeouts.Backend.MCServerTimeout;
            serverClient.SendTimeout = Config.Timeouts.Backend.MCServerTimeout;
            serverClient.ReceiveBufferSize = Config.BufferSizes.ClientBound;
            serverClient.SendBufferSize = Config.BufferSizes.ServerBound;

            // Wrap connection and initiate
            var serverConnection = MCServerConnection.GetLoggedInConnection(
                serverClient,
                clientConnection.Username,
                clientConnection.UUID,
                Translation.DefaultTranslation,
                clientboundReceiver: clientConnection);

            if (serverConnection is null) throw new Exception("Failed to get logged-in server connection");

            var mainReceiver = new MainServerboundReceiver(_serviceProvider, _serviceManager, serverConnection, clientConnection.UUID);

            return Task.FromResult<IMCClientConCallback>(
                new MCClientConCallback(
                    serverboundReceiver: mainReceiver,
                    serverConnection: serverConnection,
                    Translation.DefaultTranslation));
        }
    }
}
