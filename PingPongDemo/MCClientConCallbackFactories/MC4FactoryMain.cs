using JTJabba.EasyConfig;
using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.ServerboundReceivers;
using System.Net.Sockets;

namespace PingPongDemo.MCClientConCallbackFactories
{
    internal sealed class MC4FactoryMain : IMCClientConCallbackFactory
    {
        MainFactoryServiceContainer _serviceProvider;

        public MC4FactoryMain(ConnectionsDictionary connectionDict)
        {
            _serviceProvider = new MainFactoryServiceContainer(connectionDict);
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

            var mainReceiver = new MainServerboundReceiver(_serviceProvider, serverConnection, clientConnection.UUID);

            return Task.FromResult<IMCClientConCallback>(
                new MCClientConCallback(
                    serverboundReceiver: mainReceiver,
                    serverConnection: serverConnection, 
                    Translation.DefaultTranslation));
        }
    }
}
