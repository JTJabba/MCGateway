namespace MCGateway.Protocol
{
    public interface IMCClientConnection : IMCConnection
    {
        public delegate bool TryAddOnlinePlayer(string username, Guid uuid);
        public delegate void RemoveOnlinePlayer(Guid uuid);

        public Task ReceiveTilClosedAndDispose();
        public void Disconnect(string reason);
    }
}
