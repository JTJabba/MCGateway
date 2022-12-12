namespace MCGateway.Protocol.V759
{
    public interface IServerBoundReceiver : IDisposable
    {
        public void Forward(Packet packet);
    }
}
