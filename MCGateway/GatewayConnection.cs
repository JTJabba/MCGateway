using MCGateway.Protocol;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using JTJabba.EasyConfig;

namespace MCGateway
{
    [SkipLocalsInit]
    public sealed class GatewayConnection<GatewayConnectionCallback> : IDisposable
        where GatewayConnectionCallback : IGatewayConnectionCallback
    {
        bool _disposed = false;
        static readonly ILogger _logger = GatewayLogging.CreateLogger<GatewayConnection<GatewayConnectionCallback>>();
        readonly CancellationToken _stoppingToken;
        readonly Action<GatewayConnection<GatewayConnectionCallback>> _disposedCallback;
        
        public Guid UUID { get; init; }
        public IGatewayConnectionCallback Callback { get; init; }
        public IMCClientConnection ClientConnection { get; set; }


        public GatewayConnection(
            IMCClientConnection clientConnection,
            IGatewayConnectionCallback callback,
            Action<GatewayConnection<GatewayConnectionCallback>> disposedCallback,
            CancellationToken stoppingToken)
        {
            ClientConnection = clientConnection;
            Callback = callback;
            _disposedCallback = disposedCallback;
            _stoppingToken = stoppingToken;
            UUID = clientConnection.UUID;

            StartReceive();
        }

        public static GatewayConnection<GatewayConnectionCallback>? GetGatewayConnection(
            TcpClient tcpClient,
            Action<GatewayConnection<GatewayConnectionCallback>> disposedCallback,
            CancellationToken stoppingToken)
        {
            IMCClientConnection? clientCon = null;
            try
            {
                if (!EarlyConnectionHandler.TryHandleTilLogin<GatewayConnectionCallback>(tcpClient, out var handshake))
                {
                    tcpClient.Close();
                    return null;
                }

                var callback = GatewayConnectionCallback.GetCallback(handshake);
                clientCon = callback.GetLoggedInClientConnection(tcpClient);

                if (clientCon == null)
                {
                    return null;
                }

                try
                {
                    clientCon.Client.SendTimeout = Config.Timeouts.Clients.EnstablishedTimeout;
                    clientCon.Client.ReceiveTimeout = Config.Timeouts.Clients.EnstablishedTimeout;
                }
#if DEBUG
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Exception occured while setting up clientCon timeouts.");
                }
#else
                catch { }
#endif
                return new GatewayConnection<GatewayConnectionCallback>(
                    clientCon,
                    callback,
                    disposedCallback,
                    stoppingToken);

            }
            catch (MCConnectionClosedException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in gateway connection. Closing connection");
            }
            clientCon?.Dispose();
            return null;
        }

        void StartReceive()
        {
            Task.Run(async () =>
            {
                try { await ClientConnection.ReceiveTilClosedAndDispose(); }
                finally { Dispose(); }
            });
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
                GatewayConnectionCallback.RemoveOnlinePlayer(ClientConnection.UUID);
                ClientConnection.Dispose();
                try
                {
                    _disposedCallback(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DisposedCallback provided to GatewayConnection threw");
                }
            }
            _disposed = true;
        }
    }
}
