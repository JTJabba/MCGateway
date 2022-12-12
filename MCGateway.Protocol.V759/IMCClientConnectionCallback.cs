using System.Runtime.Versioning;
using JTJabba.EasyConfig;
using MCGateway.Protocol.V759;

namespace MCGateway.Protocol.V759
{
    public interface IMCClientConnectionCallback : IDisposable
    {
        public IMCServerConnection ServerConnection { get; }

        [RequiresPreviewFeatures]
        public static abstract IMCClientConnectionCallback GetCallback(string username, Guid uuid, Config.TranslationsObject translation, IClientBoundReceiver selfRecever);
        [RequiresPreviewFeatures]
        public static abstract Config.TranslationsObject GetTranslationsObject(Guid playerGuid);

        public void SetSkin(string skin);
        public void Forward(Packet packet);
        public void StartedReceivingCallback();
    }
}
