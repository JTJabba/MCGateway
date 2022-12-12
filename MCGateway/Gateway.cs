using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace MCGateway
{
    public sealed class Gateway<GatewayConnectionCallback> : IDisposable
        where GatewayConnectionCallback : IGatewayConnectionCallback
    {
        private bool _disposed;
        private readonly ILogger logger;
        private TcpListener tcpListener;
        private readonly CancellationToken stoppingToken;

        /// <summary>
        /// Map of client-facing ports to open connections
        /// </summary>
        public ConcurrentDictionary<ushort, GatewayConnection<GatewayConnectionCallback>> Connections { get; } = new();
        public bool IsListening { get; private set; }

        /// <summary>
        /// Creating new Gateway instance will currently reload static config and loggerFactory for all instances.
        /// Until EasyConfig is updated to support config objects and that is implemented only one instance should be created by a program
        /// </summary>
        /// <param name="configPaths"></param>
        /// <param name="stoppingToken"></param>
        /// <param name="loggerFactory"></param>
        public Gateway(string[] configPaths, CancellationToken stoppingToken, ILoggerFactory? loggerFactory = null)
        {
            if (loggerFactory != null) GatewayLogging.LoggerFactory = loggerFactory;
            logger = GatewayLogging.CreateLogger<Gateway<GatewayConnectionCallback>>();
            this.stoppingToken = stoppingToken;
            tcpListener = new TcpListener(IPAddress.Any, Config.ListeningPort);
            ConfigLoader.Load(configPaths);
        }

        [RequiresPreviewFeatures]
        public void StartListening()
        {
            if (IsListening)
                throw new InvalidOperationException("Gateway is already running");
            tcpListener = new(IPAddress.Any, Config.ListeningPort);
            tcpListener.Start();
            IsListening = true;
            _ = AcceptConnectionsAsync(tcpListener);
            logger.LogInformation("Gateway started listening on port {p}", Config.ListeningPort);
        }

        public void StopListening()
        {

            if (!IsListening)
                throw new InvalidOperationException("Gateway is not running");

            tcpListener.Stop();
            IsListening = false;
        }

        [RequiresPreviewFeatures]
        private Task AcceptConnectionsAsync(TcpListener listener)
        {
            return Task.Run(async () =>
            {
                TcpClient? client = null;
                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        client = null;
                        client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                        ConnectionAccepted(client);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed accepting TCP connection. Gateway stopped listening.");
                    client?.Close();
                    StopListening();
                }
            });
        }

        [RequiresPreviewFeatures]
        private void ConnectionAccepted(TcpClient client)
        {
            Task.Run(() =>
            {
                if (GatewayLogging.InDebug) logger.LogDebug("accepted connection");
                GatewayConnection<GatewayConnectionCallback>? gatewayConnection = null;
                try
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = Config.KeepAlive.ClientInitialTimeoutMs;
                    client.SendTimeout = Config.KeepAlive.ClientInitialTimeoutMs;
                    client.ReceiveBufferSize = Config.BufferSizes.ServerBound;
                    client.SendBufferSize = Config.BufferSizes.ClientBound;

                    gatewayConnection = GatewayConnection<GatewayConnectionCallback>.GetGatewayConnection(
                    client, GatewayConnectionDisposedCallback, stoppingToken);
                    if (gatewayConnection == null) return;
                    
                    Connections.TryAdd((ushort)((IPEndPoint)gatewayConnection.ClientConnection.Client.Client.LocalEndPoint!).Port, gatewayConnection);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error handling new connection");
                    client.Close();
                    gatewayConnection?.Dispose();
                }
            });
        }

        private void GatewayConnectionDisposedCallback(GatewayConnection<GatewayConnectionCallback> con)
        {
            Connections.Remove((ushort)((IPEndPoint)con.ClientConnection.Client.Client.LocalEndPoint!).Port, out _);
        }


        [RequiresPreviewFeatures]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        [RequiresPreviewFeatures]
        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                foreach (var connection in Connections)
                {
                    connection.Value?.Dispose();
                }
            }
            _disposed = true;
        }
    }
}
