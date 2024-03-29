﻿using JTJabba.EasyConfig;
using MCGateway.Protocol.Versions.P759_G1_19;
using MCGateway;
using System.Net.Sockets;

namespace PingPongDemo.MCClientConCallbackFactories
{
    internal sealed class MC4FactoryTesting
    {
        public MC4FactoryTesting() { }

        public Task<IMCClientConCallback> GetCallback(string username, Guid uuid, string? skin, IClientboundReceiver clientboundReceiver, CancellationToken cancellationToken)
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
            var serverConnection = MCServerConnection.GetLoggedInConnection(serverClient, username, uuid, Translation.DefaultTranslation, clientboundReceiver);

            if (serverConnection is null) throw new Exception("Failed to get logged-in server connection");

            return Task.FromResult<IMCClientConCallback>(new MCClientConCallback(
                serverboundReceiver: serverConnection,
                serverConnection: serverConnection,
                translation: Translation.DefaultTranslation));
        }
    }
}
