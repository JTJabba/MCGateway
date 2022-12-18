using System.Buffers;
using System.Runtime.CompilerServices;

namespace MCGateway.Protocol.V759.DataTypes
{
    [SkipLocalsInit]
    public sealed class PlayerPublicKey : IDisposable
    {
        bool _disposed = false;
        private readonly byte[] _data;
        readonly ushort _keyLength;
        readonly ushort _sigLength;

        public readonly long Expiry;
        public ReadOnlySpan<byte> Key { get => _data.AsSpan(0, _keyLength); }
        public ReadOnlySpan<byte> Signature { get => _data.AsSpan(_keyLength, _sigLength); }

        public PlayerPublicKey(long expiry, ReadOnlySpan<byte> key, ReadOnlySpan<byte> signature)
        {
            Expiry = expiry;

            _keyLength = (ushort)key.Length;
            _sigLength = (ushort)signature.Length;

            _data = ArrayPool<byte>.Shared.Rent(_keyLength + _sigLength);
            key.CopyTo(_data.AsSpan(0, _keyLength));
            signature.CopyTo(_data.AsSpan(_keyLength, _sigLength));
        }


        public void Dispose()
        {
            Dispose(true);
        }
        void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                ArrayPool<byte>.Shared.Return(_data);
            }
            _disposed = true;
        }
    }
}
