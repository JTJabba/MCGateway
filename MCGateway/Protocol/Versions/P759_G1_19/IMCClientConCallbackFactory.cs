namespace MCGateway.Protocol.Versions.P759_G1_19
{
    public interface IMCClientConCallbackFactory
    {
        public Task<IMCClientConCallback> GetCallback(MCClientConnection clientConnection, CancellationToken cancellationToken);
    }
}
