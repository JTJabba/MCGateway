namespace MCGateway.Protocol.Versions.P759_G1_19
{
    public interface IServerboundReceiver : IDisposable
    {
        public void Forward(Packet packet);
    }
}
