using JTJabba.EasyConfig;

namespace MCGateway.Protocol.V759
{
    public interface IMCClientConnectionCallback : IDisposable
    {
        public IMCServerConnection ServerConnection { get; }

        /// <summary>
        /// Return a callback after doing all initialization. Must complete in a timeout before a client object can be returned.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="uuid"></param>
        /// <param name="translation"></param>
        /// <param name="selfRecever"></param>
        /// <returns></returns>
        public static abstract Task<object> GetCallback
            (string username, Guid uuid, string? skin, IClientBoundReceiver selfRecever, CancellationToken cancellationToken);
        
        /// <summary>
        /// Should immediately return.
        /// </summary>
        /// <param name="playerGuid"></param>
        /// <returns></returns>
        public Config.TranslationsObject GetTranslationsObject();
        public Task SetSkin(string skin);
        public Task SetSkin(string skin, CancellationToken cancellationToken);
        public void Forward(Packet packet);
        /// <summary>
        /// Used to trigger events when a client switches to play mode. Will be used with a timeout.
        /// </summary>
        /// <returns></returns>
        public Task StartedReceivingCallback();
    }
}
