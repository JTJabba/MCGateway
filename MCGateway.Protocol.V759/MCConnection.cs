using Ionic.Zlib;
using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using LibDeflate;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public abstract class MCConnection : IMCConnection, IDisposable
    {
        private bool _disposed;
        protected bool isDisposed { get => _disposed; }
        private readonly ILogger logger = GatewayLogging.CreateLogger<MCConnection>();
        private readonly Compressor _compressor = new ZlibCompressor(Config.CompressionLevel);
        private readonly Decompressor _decompressor = new ZlibDecompressor();
        protected int _compressionThreshold = -1;
        private const int SEGMENT_MASK = 0x7F;
        private const int CONTINUE_MASK = 0x80;
        private const int MAX_VARINT_LENGTH = 32;
        protected Stream _stream;
        private readonly byte[] recvBuffer;
        private int writeCursor = 0;
        private int readCursor = 0;
        public ulong PacketsRead { get; private set; } = 0;
        public int ProtocolVersion { get { return V759Constants.ProtocolVersion; } }
        public TcpClient Client { get; init; }
        public abstract string Username { get; init; }
        public abstract Guid UUID { get; init; }
        public abstract Config.TranslationsObject ClientTranslation { get; set; }

        private static void ValidateConfig()
        {
            if (GatewayConfig.RequireCompressedFormat)
            {
                if (Config.CompressionThreshold <= 0)
                    throw new ArgumentException(
                        "GatewayConfig.RequireCompressedFormat is set to true. Config.CompressionThreshold must be greater than 0");
            }
        }
        static MCConnection()
        {
            ConfigLoader.AddOnFirstStaticLoadCallback(ValidateConfig);
        }

        protected MCConnection(TcpClient client, int bufferSize)
        {
            Client = client;
            _stream = client.GetStream();
            recvBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        }
        
        public Packet ReadPacket()
        {
            ++PacketsRead;

            int packetLength = ReadVarInt();

            // Compression disabled
            if (!GatewayConfig.RequireCompressedFormat && _compressionThreshold <= 0)
            {
                int packetLengthLength = Packet.GetVarIntLength(packetLength);
                var bytes = ReadBytesVariableOffset(packetLength, 5 + packetLengthLength);
                Packet.WriteVarInt(bytes, 5, packetLength);

                int packetIDLength = 0;
                return new Packet(
                    bytes,
                    packetLength,
                    Packet.ReadVarInt(bytes.AsSpan(5 + packetLengthLength, packetLength), ref packetIDLength),
                    packetIDLength + packetLengthLength);
            }

            int dataLength = ReadVarInt(out int dataLengthLength);

            // Data not compressed
            if (dataLength == 0)
            {
                --packetLength; // Sub dataLength length
                byte packetLengthLength = Packet.GetVarIntLength(packetLength);
                var bytes = ReadBytesVariableOffset(packetLength, 5 + packetLengthLength);
                Packet.WriteVarInt(bytes, 5, packetLength);

                int packetIDLength = 0;
                return new Packet(
                    bytes,
                    packetLength,
                    Packet.ReadVarInt(bytes.AsSpan(5 + packetLengthLength), ref packetIDLength),
                    (byte)(packetIDLength + packetLengthLength));
            }

            // Data is compressed
            packetLength -= dataLengthLength;

            int dataOffset = 5 + dataLengthLength;
            byte[] packetBytes = ArrayPool<byte>.Shared.Rent(5 + dataLengthLength + dataLength);
            byte[]? buffer = null;
            try
            {
                // Get span over compressed data. If there's room prefill and copy directly from recvBuffer
                ReadOnlySpan<byte> compressedData;
                if (recvBuffer.Length - readCursor < packetLength)
                {
                    buffer = ReadBytes5Offset(packetLength);
                    compressedData = buffer.AsSpan(5, packetLength);
                }
                else
                {
                    while (writeCursor - readCursor < packetLength)
                    {
                        int recv = _stream.Read(recvBuffer, writeCursor, recvBuffer.Length - writeCursor);
                        if (recv == 0) throw new MCConnectionClosedException();
                        writeCursor += recv;
                    }
                    compressedData = recvBuffer.AsSpan(readCursor, packetLength);
                }
                if (_decompressor.Decompress(compressedData, packetBytes.AsSpan(5 + dataLengthLength, dataLength), out _)
                    != OperationStatus.Done) throw new InvalidDataException("Decompression failed");
                

            }
            catch (Exception e)
            {
                logger.LogWarning(
                    e,
                    "Packet debug {direction} packet number 0x{count}: Exception occured while reading packet",
                    this is IMCClientConnection ? "serverbound" : "clientbound",
                    PacketsRead.ToString("X"));

                ArrayPool<byte>.Shared.Return(packetBytes);
                if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);

                throw;
            }


            byte[] ReadBytesVariableOffset(int count, int offset)
                {
                    count += offset;
                    int dataOffset = offset;
                    byte[] data = ArrayPool<byte>.Shared.Rent(count);
                    int dataRecvLength;
                    do
                    {
                        if (readCursor >= writeCursor) Recv();

                        dataRecvLength = Math.Min(count - dataOffset, writeCursor - readCursor);
                        recvBuffer.AsSpan(readCursor, dataRecvLength)
                            .CopyTo(data.AsSpan(dataOffset, dataRecvLength));
                        dataOffset += dataRecvLength;
                        readCursor += dataRecvLength;
                    } while (dataOffset < count);

                    return data;
                }
        }

        public void WritePacket(ReadOnlySpan<byte> packetBytes)
        {
            const int DATA_OFFSET = 10;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetBytes.Length + DATA_OFFSET);
            try
            {
                int uncompressedDataLengthLength;
                int packetLength;

                // Build packet with no length prefix
                if (_compressionThreshold < 0)
                {
                    packetBytes.CopyTo(buffer.AsSpan(DATA_OFFSET));
                    uncompressedDataLengthLength = 0;
                    packetLength = packetBytes.Length;
                }
                else if (packetBytes.Length > _compressionThreshold)
                {
                    try
                    {
                        int compressedDataLength = _compressor.Compress(packetBytes, buffer.AsSpan(DATA_OFFSET));
                        uncompressedDataLengthLength = Packet.GetVarIntLength(packetBytes.Length);
                        packetLength = compressedDataLength + uncompressedDataLengthLength;
                        Packet.WriteVarInt(buffer, DATA_OFFSET - uncompressedDataLengthLength, packetBytes.Length);
                    }
                    catch (Exception e)
                    {
                        if (GatewayLogging.InDebug) logger.LogWarning(e, "Unable to compress packet");
                        packetBytes.CopyTo(buffer.AsSpan(DATA_OFFSET));
                        uncompressedDataLengthLength = 1;
                        buffer[DATA_OFFSET - 1] = 0;
                        packetLength = packetBytes.Length + 1;
                    }
                }
                else
                {
                    packetBytes.CopyTo(buffer.AsSpan(DATA_OFFSET));
                    uncompressedDataLengthLength = 1;
                    buffer[DATA_OFFSET - 1] = 0;
                    packetLength = packetBytes.Length + 1;
                }

                // Length prefix and write. Note packetLength includes uncompressedDataLength length
                int packetLengthLength = Packet.GetVarIntLength(packetLength);
                Packet.WriteVarInt(buffer, DATA_OFFSET - uncompressedDataLengthLength - packetLengthLength, packetLength);
                //logger.LogDebug("Sending packet " + Convert.ToHexString(buffer.AsSpan(DATA_OFFSET - uncompressedDataLengthLength - packetLengthLength, Math.Min(packetLengthLength + packetLength, 32))));
                _stream.Write(buffer.AsSpan(DATA_OFFSET - uncompressedDataLengthLength - packetLengthLength, packetLengthLength + packetLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected int ReadVarInt()
        {
            int value = 0;
            int length = 0;
            int currentByte;
            while (true)
            {
                currentByte = ReadByte();
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        protected int ReadVarInt(out int read)
        {
            int value = 0;
            int length = 0;
            int currentByte;
            read = 0;
            while (true)
            {
                currentByte = ReadByte();
                ++read;
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        /// <summary>
        /// Read bytes into a buffer with a 5 byte offset to support length prefixing
        /// </summary>
        /// <param name="length"></param>
        /// <returns>A byte array rented from ArrayPool Shared. Should be returned</returns>
        protected byte[] ReadBytes5Offset(int count)
        {
            count += 5;
            int dataOffset = 5;
            byte[] data = ArrayPool<byte>.Shared.Rent(count);
            int dataRecvLength;
            do
            {
                if (readCursor >= writeCursor) Recv();

                dataRecvLength = Math.Min(count - dataOffset, writeCursor - readCursor);
                recvBuffer.AsSpan(readCursor, dataRecvLength)
                    .CopyTo(data.AsSpan(dataOffset, dataRecvLength));
                dataOffset += dataRecvLength;
                readCursor += dataRecvLength;
            } while (dataOffset < count);

            return data;
        }

        protected int ReadByte()
        {
            if (readCursor >= writeCursor) Recv();
            return recvBuffer[readCursor++];
        }

        private void Recv()
        {
            readCursor = 0;
            writeCursor = _stream.Read(recvBuffer, 0, recvBuffer.Length);
            if (writeCursor == 0) throw new MCConnectionClosedException();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                Client.Close();
                _compressor.Dispose();
                _decompressor.Dispose();
                _stream.Dispose();
                ArrayPool<byte>.Shared.Return(recvBuffer);
            }
            _disposed = true;
        }
    }
}
