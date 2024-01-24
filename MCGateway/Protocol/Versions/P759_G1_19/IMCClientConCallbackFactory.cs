namespace MCGateway.Protocol.Versions.P759_G1_19
{
    public interface IMCClientConCallbackFactory
    {
        public Task<IMCClientConCallback> GetCallback(string username, Guid uuid, string? skin, IClientboundReceiver clientboundReceiver, CancellationToken cancellationToken);
    }
}
