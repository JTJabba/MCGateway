namespace MCGateway.Protocol.V759
{
    public interface IServerboundReceiver : IDisposable
    {
        public void Forward(Packet packet);
    }
}
