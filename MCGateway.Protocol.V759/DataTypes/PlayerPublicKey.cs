namespace MCGateway.Protocol.V759.DataTypes
{
    public class PlayerPublicKey
    {
        public readonly long Expiry;
        public readonly byte[] Key;
        public readonly byte[] Signature;

        public PlayerPublicKey(long expiry, ReadOnlySpan<byte> key, ReadOnlySpan<byte> signature)
        {
            Expiry = expiry;
            Key = key.ToArray();
            Signature = signature.ToArray();
        }
    }
}
