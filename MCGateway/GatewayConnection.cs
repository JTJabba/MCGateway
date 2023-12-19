using MCGateway.Protocol;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using JTJabba.EasyConfig;

namespace MCGateway
{
    [SkipLocalsInit]
    public sealed class GatewayConnection : IDisposable
    {
        bool _disposed = false;
        static readonly ILogger _logger = GatewayLogging.CreateLogger<GatewayConnection>();
        IGatewayConnectionCallback _callback;
        readonly Action<GatewayConnection> _disposedCallback;
        
        public Guid UUID { get; init; }
        public IMCClientConnection ClientConnection { get; set; }


        GatewayConnection(
            IMCClientConnection clientConnection,
            IGatewayConnectionCallback callback,
            Action<GatewayConnection> disposedCallback)
        {
            ClientConnection = clientConnection;
            _callback = callback;
            _disposedCallback = disposedCallback;
            UUID = clientConnection.UUID;

            StartReceive();
        }

        public static GatewayConnection? GetGatewayConnection(
            TcpClient tcpClient,
            IGatewayConnectionCallback callback,
            Action<GatewayConnection> disposedCallback)
        {
            IMCClientConnection? clientCon = null;
            try
            {
                if (!EarlyConnectionHandler.TryHandleTilLogin(tcpClient, callback, out var handshake))
                {
                    tcpClient.Close();
                    return null;
                }

                clientCon = callback.GetLoggedInClientConnection(handshake, tcpClient);

                if (clientCon == null)
                {
                    tcpClient.Close();
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
                return new GatewayConnection(
                    clientCon,
                    callback,
                    disposedCallback);

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
                _callback.RemoveOnlinePlayer(ClientConnection.UUID);
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
