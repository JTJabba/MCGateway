using JTJabba.EasyConfig;
using MCGateway;
using MCGateway.Protocol;
using MCGateway.Protocol.Versions.P759_G1_19;

namespace PingPongDemo
{
    internal sealed class MCClientConCallback : IMCClientConCallback
    {
        bool _disposed;
        ILogger _logger = GatewayLogging.CreateLogger<MCClientConCallback>();
        IServerboundReceiver _serverboundReceiver;
        public IMCServerConnection ServerConnection { get; private set; }
        public Config.TranslationsObject Translation { get; }

        internal MCClientConCallback(IServerboundReceiver serverboundReceiver, IMCServerConnection serverConnection, Config.TranslationsObject translation)
        {
            _serverboundReceiver = serverboundReceiver;
            ServerConnection = serverConnection;
            Translation = translation;
        }

        public Config.TranslationsObject GetTranslationsObject()
        {
            return MCGateway.Translation.DefaultTranslation;
        }

        public void Forward(Packet packet)
        {
            _serverboundReceiver.Forward(packet);
        }

        public void SetSkin(string skin)
        {
        }

        public void StartedReceivingCallback()
        {
            ServerConnection.ReceiveTilClosedAndDispose();
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
