using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MCGateway
{
    public sealed class Gateway<GatewayConnectionCallback> : IDisposable
        where GatewayConnectionCallback : IGatewayConnectionCallback
    {
        bool _disposed = false;
        readonly ILogger _logger;
        TcpListener tcpListener;
        readonly CancellationToken stoppingToken;

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
            _logger = GatewayLogging.CreateLogger<Gateway<GatewayConnectionCallback>>();
            this.stoppingToken = stoppingToken;
            tcpListener = new TcpListener(IPAddress.Any, Config.ListeningPort);
            ConfigLoader.Load(configPaths);
        }

        public void StartListening()
        {
            if (IsListening)
                throw new InvalidOperationException("Gateway is already running");
            tcpListener = new(IPAddress.Any, Config.ListeningPort);
            tcpListener.Start();
            IsListening = true;
            _ = AcceptConnectionsAsync(tcpListener);
            _logger.LogInformation("Gateway started listening on port {p}", Config.ListeningPort);
        }

        public void StopListening()
        {

            if (!IsListening)
                throw new InvalidOperationException("Gateway is not running");

            IsListening = false;
            tcpListener.Stop();
        }

        Task AcceptConnectionsAsync(TcpListener listener)
        {
            return Task.Run(async () =>
            {
                TcpClient? client = null;
                try
                {
                    while (!stoppingToken.IsCancellationRequested && IsListening)
                    {
                        client = null;
                        client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                        ConnectionAccepted(client);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed accepting TCP connection. Gateway stopped listening.");
                    client?.Close();
                    StopListening();
                }
            });
        }

        void ConnectionAccepted(TcpClient client)
        {
            Task.Run(() =>
            {
                GatewayConnection<GatewayConnectionCallback>? gatewayConnection = null;
                try
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = Config.Timeouts.Clients.InitialTimeout;
                    client.SendTimeout = Config.Timeouts.Clients.InitialTimeout;
                    client.ReceiveBufferSize = Config.BufferSizes.ServerBound;
                    client.SendBufferSize = Config.BufferSizes.ClientBound;

                    gatewayConnection = GatewayConnection<GatewayConnectionCallback>.GetGatewayConnection(
                    client, GatewayConnectionDisposedCallback, stoppingToken);
                    if (gatewayConnection == null) return;
                    
                    Connections.TryAdd(gatewayConnection.Port, gatewayConnection);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error handling new connection");
                    client.Close();
                    gatewayConnection?.Dispose();
                }
            });
        }

        void GatewayConnectionDisposedCallback(GatewayConnection<GatewayConnectionCallback> con)
        {
            Connections.Remove(con.Port, out _);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
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
