namespace MCGateway.Protocol
{
    public interface IMCServerConnection : IMCConnection
    {
        public Task ReceiveTilClosed();
    }
}
