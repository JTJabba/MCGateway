using MCGateway.Protocol;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using JTJabba.EasyConfig;

namespace MCGateway
{
    [SkipLocalsInit]
    public sealed class GatewayConnection<GatewayConnectionCallback> : IDisposable
        where GatewayConnectionCallback : IGatewayConnectionCallback
    {
        bool _disposed = true;
        static readonly ILogger logger = GatewayLogging.CreateLogger<GatewayConnection<GatewayConnectionCallback>>();
        readonly CancellationToken _stoppingToken;
        readonly Action<GatewayConnection<GatewayConnectionCallback>> _disposedCallback;
        
        public IGatewayConnectionCallback Callback { get; init; }
        public IMCClientConnection ClientConnection { get; set; }

        [RequiresPreviewFeatures]
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

            StartReceive();
        }

        [RequiresPreviewFeatures]
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
                    if (GatewayLogging.InDebug) logger.LogDebug("Connection closed");
                    tcpClient.Close();
                    return null;
                }

                if (GatewayLogging.InDebug) logger.LogDebug("Login requested");
                var callback = GatewayConnectionCallback.GetCallback(handshake);
                clientCon = callback.GetLoggedInClientConnection(tcpClient);

                if (clientCon == null)
                {
                    if (GatewayLogging.InDebug) logger.LogDebug("Gateway got null client connection");
                    return null;
                }

                if (GatewayLogging.InDebug) logger.LogDebug("Gateway got client connection");
                if (!GatewayConnectionCallback.TryAddOnlinePlayer(clientCon.Username, clientCon.UUID))
                {
                    clientCon.Disconnect(clientCon.ClientTranslation.DisconnectPlayerAlreadyOnline);
                    clientCon.Dispose();
                    return null;
                }

                clientCon.Client.SendTimeout = Config.KeepAlive.ClientEnstablishedTimeoutMs;
                clientCon.Client.ReceiveTimeout = Config.KeepAlive.ClientEnstablishedTimeoutMs;

                return new GatewayConnection<GatewayConnectionCallback>(
                    clientCon,
                    callback,
                    disposedCallback,
                    stoppingToken);

            }
            catch (MCConnectionClosedException e)
            {
                if (GatewayLogging.InDebug) logger.LogDebug(e, "MC connection closed");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled error in gateway connection. Closing connection");
            }
            clientCon?.Dispose();
            return null;
        }

        [RequiresPreviewFeatures]
        void StartReceive()
        {
            Task.Run(async () =>
            {
                try { await ClientConnection.ReceiveTilClosedAndDispose(); }
                finally { Dispose(); }
            });
        }


        [RequiresPreviewFeatures]
        public void Dispose()
        {
            if (GatewayLogging.InDebug) logger.LogInformation("Gateway connection cleaning up");
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        [RequiresPreviewFeatures]
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
                catch (Exception e)
                {
                    logger.LogError(e, "DisposedCallback provided to GatewayConnection threw");
                }
            }
            _disposed = true;
        }
    }
}
