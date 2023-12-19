namespace MCGateway.DataTypes
{
    public readonly struct Handshake
    {
        public readonly string ServerAddress;
        public readonly ushort ServerPort;
        public readonly int ProtocolVersion;

        public Handshake(string serverAddress, ushort serverPort, int protocolVersion)
        {
            ServerAddress = serverAddress;
            ServerPort = serverPort;
            ProtocolVersion = protocolVersion;
        }
    }
}
