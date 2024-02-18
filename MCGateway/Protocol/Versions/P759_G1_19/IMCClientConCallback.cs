using JTJabba.EasyConfig;

namespace MCGateway.Protocol.Versions.P759_G1_19
{
    public interface IMCClientConCallback : IDisposable
    {
        /// <summary>
        /// Should immediately return.
        /// </summary>
        /// <returns></returns>
        public Config.TranslationsObject Translation { get; }
        public void SetSkin(string skin);
        public void Forward(Packet packet);
        /// <summary>
        /// Used to trigger events when a client switches to play mode.
        /// </summary>
        /// <returns></returns>
        public void StartedReceivingCallback();
    }
}
