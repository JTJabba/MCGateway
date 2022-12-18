using System.Buffers;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MCGateway.Protocol.Crypto
{
    // Adapted from https://github.com/MCCTeam/Minecraft-Console-Client/blob/0907958ded2c5ac31f68bce4ed0ec8e246e2581e/MinecraftClient/Crypto/AesCfb8Stream.cs
    [SkipLocalsInit]
    public sealed class FastAesCfb8Stream : Stream
    {
        readonly FastAes _fastAes;
        bool inStreamEnded = false;
        readonly byte[] _readStreamIV = new byte[BlockSize];
        readonly byte[] _writeStreamIV = new byte[BlockSize];

        public const int BlockSize = 16;
        public static readonly bool IsSupported = FastAes.IsSupported();

        public Stream BaseStream { get; set; }

        public FastAesCfb8Stream(Stream stream, Span<byte> key)
        {
            BaseStream = stream;
            _fastAes = new FastAes(key);

            key.CopyTo(_readStreamIV);
            key.CopyTo(_writeStreamIV);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int ReadByte()
        {
            if (inStreamEnded)
                return -1;

            int inputBuf = BaseStream.ReadByte();
            if (inputBuf == -1)
            {
                inStreamEnded = true;
                return -1;
            }

            Span<byte> blockOutput = stackalloc byte[BlockSize];
            _fastAes.EncryptEcb(_readStreamIV, blockOutput);

            // Shift left
            Array.Copy(_readStreamIV, 1, _readStreamIV, 0, BlockSize - 1);
            _readStreamIV[BlockSize - 1] = (byte)inputBuf;

            return (byte)(blockOutput[0] ^ inputBuf);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Read(byte[] buffer, int outOffset, int count)
        {
            if (inStreamEnded)
                return 0;

            Span<byte> blockOutput = stackalloc byte[BlockSize];

            byte[] inputBuf = ArrayPool<byte>.Shared.Rent(BlockSize + count);
            try
            {
                Array.Copy(_readStreamIV, inputBuf, BlockSize);
                
                int read = BaseStream.Read(inputBuf, BlockSize, count);
                if (read == 0)
                {
                    inStreamEnded = true;
                    return read;
                }
                
                for (int idx = 0; idx < read; ++idx)
                {
                    ReadOnlySpan<byte> blockInput = new(inputBuf, idx, BlockSize);
                    _fastAes.EncryptEcb(blockInput, blockOutput);
                    buffer[outOffset + idx] = (byte)(blockOutput[0] ^ inputBuf[idx + BlockSize]);
                }

                Array.Copy(inputBuf, read, _readStreamIV, 0, BlockSize);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(inputBuf);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void WriteByte(byte b)
        {
            Span<byte> blockOutput = stackalloc byte[BlockSize];

            _fastAes.EncryptEcb(_writeStreamIV, blockOutput);

            byte outputBuf = (byte)(blockOutput[0] ^ b);

            BaseStream.WriteByte(outputBuf);

            // Shift left
            Array.Copy(_writeStreamIV, 1, _writeStreamIV, 0, BlockSize - 1);
            _writeStreamIV[BlockSize - 1] = outputBuf;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override void Write(byte[] input, int offset, int required)
        {
            byte[] outputBuf = ArrayPool<byte>.Shared.Rent(BlockSize + required);
            try
            {
                Array.Copy(_writeStreamIV, outputBuf, BlockSize);

                Span<byte> blockOutput = stackalloc byte[BlockSize];
                for (int written = 0; written < required; ++written)
                {
                    ReadOnlySpan<byte> blockInput = new(outputBuf, written, BlockSize);
                    _fastAes.EncryptEcb(blockInput, blockOutput);
                    outputBuf[BlockSize + written] = (byte)(blockOutput[0] ^ input[offset + written]);
                }

                BaseStream.WriteAsync(outputBuf, BlockSize, required);

                Array.Copy(outputBuf, required, _writeStreamIV, 0, BlockSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outputBuf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) BaseStream.Dispose();
        }
    }
}