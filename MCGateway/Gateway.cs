using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MCGateway
{
    public sealed class Gateway : IDisposable
    {
        bool _disposed = false;
        readonly ILogger _logger;
        IGatewayConnectionCallback _callback;
        TcpListener _tcpListener;

        /// <summary>
        /// Map of client-UUIDs to open connections
        /// </summary>
        public ConcurrentDictionary<Guid, GatewayConnection> Connections { get; } = new();
        public bool IsListening { get; private set; }

        /// <summary>
        /// Creating new Gateway instance will currently reload static config and loggerFactory for all instances.
        /// Until EasyConfig is updated to support config objects and that is implemented only one instance should be created by a program
        /// </summary>
        /// <param name="configPaths"></param>
        /// <param name="loggerFactory"></param>
        public Gateway(IGatewayConnectionCallback callback, string[] configPaths, ILoggerFactory? loggerFactory = null)
        {
            if (loggerFactory != null) GatewayLogging.LoggerFactory = loggerFactory;
            _logger = GatewayLogging.CreateLogger<Gateway>();
            _callback = callback;
            _tcpListener = new TcpListener(IPAddress.Any, Config.ListeningPort);
            ConfigLoader.Load(configPaths);
        }

        public void StartListening()
        {
            if (IsListening)
                throw new InvalidOperationException("Gateway is already running");
            _tcpListener = new(IPAddress.Any, Config.ListeningPort);
            _tcpListener.Start();
            IsListening = true;
            _ = AcceptConnectionsAsync(_tcpListener);
            _logger.LogInformation("Gateway started listening on port {p}", Config.ListeningPort);
        }

        public void StopListening()
        {

            if (!IsListening)
                return;

            IsListening = false;
            _tcpListener.Stop();
        }

        Task AcceptConnectionsAsync(TcpListener listener)
        {
            return Task.Run(async () =>
            {
                TcpClient? client = null;
                try
                {
                    while (IsListening)
                    {
                        client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        ConnectionAccepted(client);
                        client = null;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is SocketException && !IsListening) return; // Handles stopped listener
                    _logger.LogError(ex, "Failed accepting TCP connection. Gateway stopped listening.");
                }
                finally
                {
                    client?.Close();
                    StopListening();
                }
            });
        }

        void ConnectionAccepted(TcpClient client)
        {
            Task.Run(() =>
            {
                GatewayConnection? gatewayConnection = null;
                try
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = Config.Timeouts.Clients.InitialTimeout;
                    client.SendTimeout = Config.Timeouts.Clients.InitialTimeout;
                    client.ReceiveBufferSize = Config.BufferSizes.ServerBound;
                    client.SendBufferSize = Config.BufferSizes.ClientBound;

                    gatewayConnection = GatewayConnection.GetGatewayConnection(
                    client, _callback, GatewayConnectionDisposedCallback);
                    if (gatewayConnection == null) return;
                    
                    Connections.TryAdd(gatewayConnection.UUID, gatewayConnection);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error handling new connection");
                    client.Close();
                    gatewayConnection?.Dispose();
                }
            });
        }

        void GatewayConnectionDisposedCallback(GatewayConnection con)
        {
            Connections.Remove(con.UUID, out _);
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
                StopListening();
                foreach (var connection in Connections)
                {
                    connection.Value?.Dispose();
                }
            }
            _disposed = true;
        }
    }
}
