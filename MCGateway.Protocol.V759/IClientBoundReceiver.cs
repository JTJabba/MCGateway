namespace MCGateway.Protocol.V759
{
    public interface IClientBoundReceiver : IDisposable
    {
        public void Forward(Packet packet);
    }
}
