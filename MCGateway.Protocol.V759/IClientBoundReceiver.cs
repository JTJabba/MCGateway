namespace MCGateway.Protocol.V759
{
    public interface IClientboundReceiver : IDisposable
    {
        public void Forward(Packet packet);
    }
}
