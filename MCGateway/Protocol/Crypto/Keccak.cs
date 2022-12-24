using Org.BouncyCastle.Crypto.Digests;
using System.Text;

namespace MCGateway.Protocol.Crypto
{
    public static class Keccak
    {
        public static Guid HashStringToGuid(string str)
        {
            Guid guid;
            var bytes = Encoding.UTF8.GetBytes(str);

            var digest = new KeccakDigest(128);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            var hash = new byte[16]; // Must be exactly 16
            digest.DoFinal(hash, 0);
            guid = new Guid(hash);
            return guid;
        }
    }
}
