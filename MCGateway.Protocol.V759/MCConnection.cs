using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using LibDeflate;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public abstract class MCConnection : IMCConnection, IDisposable
    {
        bool _disposed = false;
        protected bool isDisposed { get => _disposed; }
        readonly ILogger _logger = GatewayLogging.CreateLogger<MCConnection>();
        readonly Compressor _compressor = new ZlibCompressor(Config.CompressionLevel);
        readonly Decompressor _decompressor = new ZlibDecompressor();
        protected int _compressionThreshold = -1;
        const int SEGMENT_MASK = 0x7F;
        const int CONTINUE_MASK = 0x80;
        const int MAX_VARINT_LENGTH = 32;
        protected Stream _stream;
        readonly byte[] recvBuffer;
        int writeCursor = 0;
        int readCursor = 0;

        public ulong InitTimestamp { get; init; }
        public ulong PacketsRead { get; private set; } = 0;
        public ulong PacketsWrite { get; private set; } = 0;
        public int ProtocolVersion { get { return V759Constants.ProtocolVersion; } }
        public TcpClient Client { get; init; }
        public abstract string Username { get; init; }
        public abstract Guid UUID { get; init; }
        public abstract Config.TranslationsObject ClientTranslation { get; set; }


        protected MCConnection(TcpClient client, int bufferSize, ulong ititializedTimestamp)
        {
            Client = client;
            _stream = client.GetStream();
            recvBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            InitTimestamp = ititializedTimestamp;
        }
        
        // TODO in future for speeding up reading large packets,
        // look into receiving directly into packet's buffer,
        // or storing spans over data in recv buffer (avoid if possible)
        public Packet ReadPacket()
        {
            ++PacketsRead;
            
            int packetIDLength = 0;
            int packetLength = ReadVarInt(out int packetLengthLength);

            // Compression disabled
            if (!GatewayConfig.RequireCompressedFormat && _compressionThreshold < 0)
                return ReadUncompressedPacket();

            int dataLength = ReadVarInt(out int dataLengthLength);

            // Data not compressed
            if (dataLength == 0)
            {
                --packetLength;
                return ReadUncompressedPacket();
            }

            // Data is compressed
            int compressedPacketOffset = Packet.SCRATCHSPACE + dataLength;
            int compressedDataLength = packetLength - dataLengthLength;
            byte[] data = ArrayPool<byte>.Shared.Rent(compressedPacketOffset + packetLengthLength + packetLength);
            try
            {
                // Write compressed packet to its spot in data array
                Packet.WriteVarInt(data, compressedPacketOffset, packetLength);
                int compressedDataOffset = compressedPacketOffset + packetLengthLength;
                Packet.WriteVarInt(data, compressedDataOffset, dataLength);
                compressedDataOffset += dataLengthLength;
                ReadBytesToBuffer(data, compressedDataOffset, compressedDataLength);
                
                // Decompress data
                if (_decompressor.Decompress(
                    data.AsSpan(compressedDataOffset, compressedDataLength),
                    data.AsSpan(Packet.SCRATCHSPACE, dataLength),
                    out _)
                    != OperationStatus.Done) throw new InvalidDataException("Decompression failed");

                return new Packet(
                    data,
                    Packet.SCRATCHSPACE + dataLength,
                    packetLengthLength + packetLength,
                    Packet.ReadVarInt(data.AsSpan(Packet.SCRATCHSPACE, dataLength), ref packetIDLength),
                    packetIDLength);

            }
            catch (Exception ex) when (
                ex is not MCConnectionClosedException &&
                ex is not InvalidDataException)
            {
                ArrayPool<byte>.Shared.Return(data);

                GatewayLogging.LogPacket(
                    _logger,
                    LogLevel.Warning,
                    this,
                    "Exception occured while reading packet",
                    null,
                    ex);

                throw new MCConnectionClosedException();
            }


            Packet ReadUncompressedPacket()
            {
                int uncompressedEndOffset = Packet.SCRATCHSPACE + packetLength;
                var data = ArrayPool<byte>.Shared.Rent(uncompressedEndOffset);
                try
                {

                    ReadBytesToBuffer(data, Packet.SCRATCHSPACE, packetLength);
                    return new Packet(
                        data,
                        uncompressedEndOffset,
                        0,
                        Packet.ReadVarInt(data.AsSpan(Packet.SCRATCHSPACE, packetLength), ref packetIDLength),
                        packetIDLength);
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(data);
                    throw;
                }
            }
        }

        /// <summary>
        /// Writes and disposes a packet. Modifies packet so WILL DISPOSE ON EXCEPTION
        /// </summary>
        /// <param name="packet"></param>
        public void WritePacket(Packet packet)
        {
            ++PacketsWrite;

            try
            {
                Span<byte> rawPacket;

                // Compression disabled
                if (!GatewayConfig.RequireCompressedFormat && _compressionThreshold < 0)
                {
                    int packetLengthLength = Packet.GetVarIntLength(packet.PacketIDAndDataLength);
                    rawPacket =
                        packet.Data.AsSpan(
                            Packet.SCRATCHSPACE - packetLengthLength,
                            packet.PacketIDAndDataLength);
                    Packet.WriteVarInt(rawPacket, packet.PacketIDAndDataLength);
                }

                // Not compressed
                else if (packet.PacketIDAndDataLength < _compressionThreshold)
                {
                    int packetLength = packet.PacketIDAndDataLength + 1;
                    int packetLengthLength = Packet.GetVarIntLength(packetLength);
                    packet.Data[Packet.SCRATCHSPACE - 1] = 0;
                    rawPacket =
                        packet.Data.AsSpan(
                            Packet.SCRATCHSPACE - 1 - packetLengthLength,
                            packetLengthLength + packetLength);
                    Packet.WriteVarInt(rawPacket, packetLength);
                }

                // Compressed
                else if (packet.RawCompressedPacketLength != 0)
                {
                    rawPacket =
                        packet.Data.AsSpan(
                            packet.RawCompressedPacketOffset,
                            packet.RawCompressedPacketLength);
                }
                else
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(packet.RawCompressedPacketOffset);
                    try
                    {
                        const int HEADER_SCRATCHSPACE = 10;

                        int compressedDataLength =
                            _compressor.Compress(
                                packet.Data.AsSpan(
                                    Packet.SCRATCHSPACE,
                                    packet.PacketIDAndDataLength),
                                buffer.AsSpan(HEADER_SCRATCHSPACE));

                        int dataLengthLength = Packet.GetVarIntLength(packet.PacketIDAndDataLength);
                        int packetLength = dataLengthLength + compressedDataLength;
                        int dataLengthOffset = HEADER_SCRATCHSPACE - dataLengthLength;
                        int packetLengthLength = Packet.GetVarIntLength(packetLength);

                        Packet.WriteVarInt(
                            buffer,
                            dataLengthOffset,
                            packet.PacketIDAndDataLength);

                        rawPacket =
                            packet.Data.AsSpan(
                                dataLengthOffset - packetLengthLength,
                                packetLengthLength + packetLength);

                        Packet.WriteVarInt(rawPacket, packetLength);

                        _stream.Write(rawPacket);
                        return;
                    }
                    catch
                    {
                        // Should log and write uncompressed
                        throw; // remove when wroted
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                _stream.Write(rawPacket);
            }
            finally
            {
                packet.Dispose();
            }
        }

        /// <summary>
        /// Always checks if compression is enabled even if GatewayConfig.RequireCompressedFormat is set
        /// </summary>
        /// <returns></returns>
        protected Packet ReadPacketLogin()
        {
            if (_compressionThreshold >= 0)
            {
                return ReadPacket();
            }

            ++PacketsRead;

            int packetLength = ReadVarInt();
            var data = ArrayPool<byte>.Shared.Rent(Packet.SCRATCHSPACE + packetLength);
            try
            {
                int packetIDLength = 0;
                ReadBytesToBuffer(data, Packet.SCRATCHSPACE, packetLength);
                return new Packet(
                    data,
                    Packet.SCRATCHSPACE + packetLength,
                    0,
                    Packet.ReadVarInt(data.AsSpan(Packet.SCRATCHSPACE, packetLength), ref packetIDLength),
                    packetIDLength);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(data);
                throw;
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

        protected byte[] ReadBytesToBuffer(byte[] buffer, int offset, int count)
        {
            count += offset;
            int dataOffset = offset;
            int dataRecvLength;
            do
            {
                if (readCursor >= writeCursor) Recv();

                dataRecvLength = Math.Min(count - dataOffset, writeCursor - readCursor);
                recvBuffer.AsSpan(readCursor, dataRecvLength)
                    .CopyTo(buffer.AsSpan(dataOffset, dataRecvLength));
                dataOffset += dataRecvLength;
                readCursor += dataRecvLength;
            } while (dataOffset < count);

            return buffer;
        }

        protected int ReadByte()
        {
            if (readCursor >= writeCursor) Recv();
            return recvBuffer[readCursor++];
        }

        void Recv()
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
