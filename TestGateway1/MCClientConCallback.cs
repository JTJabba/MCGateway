﻿using System;
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
        IServerBoundReceiver _receiver;
        public IMCServerConnection ServerConnection { get; private set; }

        public MCClientConCallback(
            string username, Guid uuid, string? skin, IClientBoundReceiver selfReceiver, CancellationToken cancellationToken)
        {
            var serverClient = new TcpClient();
            serverClient.ConnectAsync("192.168.0.72", 25565, cancellationToken).AsTask().Wait(cancellationToken);
            serverClient.NoDelay = true;
            serverClient.ReceiveTimeout = Config.KeepAlive.ServerTimeoutMs;
            serverClient.SendTimeout = Config.KeepAlive.ServerTimeoutMs;
            serverClient.ReceiveBufferSize = Config.BufferSizes.ClientBound;
            serverClient.SendBufferSize = Config.BufferSizes.ServerBound;
            var serverConnection = new MCServerConnection(serverClient, username, uuid, GetTranslationsObject(), selfReceiver);
            _receiver = serverConnection;
            ServerConnection = serverConnection;
        }
        [RequiresPreviewFeatures]
        public static Task<object> GetCallback(
            string username, Guid uuid, string? skin, IClientBoundReceiver selfReceiver, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return (object)(new MCClientConCallback(username, uuid, skin, selfReceiver, cancellationToken));
            });
        }

        public Config.TranslationsObject GetTranslationsObject()
        {
            return Translation.DefaultTranslation;
        }

        public void Forward(Packet packet)
        {
            _receiver.Forward(packet);
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

        public Task StartedReceivingCallback()
        {
            ServerConnection.ReceiveTilClosedAndDispose();
            return Task.CompletedTask;
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
