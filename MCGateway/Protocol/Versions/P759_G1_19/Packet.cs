using MCGateway.Protocol.Versions.P759_G1_19.DataTypes;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace MCGateway.Protocol.Versions.P759_G1_19
{
    [SkipLocalsInit]
    public ref struct Packet
    {
        public const int SCRATCHSPACE = 6;

        bool _disposed = false;

        /// <summary>
        /// Stores scratchspace bytes, decompressed packetID and data, then optionally packet in compressed form
        /// </summary>
        internal readonly byte[] Data { get; init; }
        /// <summary>
        /// Compressed packet offset (or index after packet data) in Data
        /// </summary>
        public readonly int RawCompressedPacketOffset { get; init; }
        /// <summary>
        /// Zero if no compressed data
        /// </summary>
        public readonly int RawCompressedPacketLength { get; init; }
        public readonly int PacketIDAndDataLength { get => RawCompressedPacketOffset - SCRATCHSPACE; }
        public readonly int PacketID { get; init; }
        int _dataFieldOffset;
        public ReadOnlySpan<byte> DataField { get => Data.AsSpan(_dataFieldOffset, RawCompressedPacketOffset); }
        /// <summary>
        /// CursorPosition is initialized at beginning of data, after <c>PacketID</c>.
        /// Use MoveCursor to adjust cursor (Compatibility in using blocks)
        /// </summary>
        public int CursorPosition { get; private set; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveCursor(int x) => CursorPosition += x;


        /// <summary>
        /// If <c>data</c> is not rented from ArrayPool.Shared, do not dispose packet.
        /// Should contain PacketID + data starting at Packet.SCRATCHSPACE,
        /// and optionally a raw compressed packet including packet length prefix
        /// </summary>
        /// <param name="data"></param>
        /// <param name="rawCompressedPacketOffset"></param>
        /// <param name="rawCompressedPacketLength"></param>
        /// <param name="packetID"></param>
        /// <param name="packetIDLength"></param>
        public Packet(byte[] data, int rawCompressedPacketOffset, int rawCompressedPacketLength, int packetID, int packetIDLength)
        {
            Data = data;
            RawCompressedPacketOffset = rawCompressedPacketOffset;
            RawCompressedPacketLength = rawCompressedPacketLength;
            PacketID = packetID;
            _dataFieldOffset = SCRATCHSPACE + packetIDLength;
            CursorPosition = _dataFieldOffset;
        }


        // Static helper methods for reading/writing packets from/to byte arrays
        #region "STATIC_HELPER_METHODS"


        /// <summary>
        /// Reads a VarInt and advances the offset.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public static int ReadVarInt(ReadOnlySpan<byte> buffer, ref int offset)
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;
            const int MAX_VARINT_LENGTH = 32;
            int value = 0;
            int length = 0;
            int currentByte;

            while (true)
            {
                currentByte = buffer[offset++];
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        public static int ReadVarInt(ReadOnlySpan<byte> buffer)
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;
            const int MAX_VARINT_LENGTH = 32;
            int value = 0;
            int offset = 0;
            int length = 0;
            int currentByte;

            while (true)
            {
                currentByte = buffer[offset++];
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        /// <summary>
        /// Writes VarInt to buffer.
        /// </summary>
        /// <returns>The number of bytes written</returns>
        public static int WriteVarInt(Span<byte> buffer, int offset, int value)
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;

            int bytesWritten = 0;
            while (true)
            {
                if ((value & 0xFFFFFF80) == 0)
                {
                    buffer[offset + bytesWritten++] = (byte)value;
                    return bytesWritten;
                }
                buffer[offset + bytesWritten++] = (byte)(value & SEGMENT_MASK | CONTINUE_MASK);
                value >>>= 7;
            }
        }

        /// <summary>
        /// Writes VarInt to buffer.
        /// </summary>
        /// <returns>The number of bytes written</returns>
        public static int WriteVarInt(Span<byte> buffer, int value)
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;

            int bytesWritten = 0;
            while (true)
            {
                if ((value & 0xFFFFFF80) == 0)
                {
                    buffer[bytesWritten++] = (byte)value;
                    return bytesWritten;
                }
                buffer[bytesWritten++] = (byte)(value & SEGMENT_MASK | CONTINUE_MASK);
                value >>>= 7;
            }
        }

        public static void WriteLongBigEndian(long value, Span<byte> target)
        {
            for (int i = 0; i < 8; i++)
                target[i] = (byte)(value >> (56 - 8 * i));
        }

        /// <summary>
        /// Writes Guid to buffer.
        /// </summary>
        /// <returns>The number of bytes written</returns>
        public static void WriteGuidBigEndian(Guid guid, Span<byte> target)
        {
            byte[] bytes = guid.ToByteArray();

            // Minecraft uses Big Endian, split into two longs: most and least significant bits
            long mostSigBits = BitConverter.ToInt64(bytes, 0);
            long leastSigBits = BitConverter.ToInt64(bytes, 8);

            // Write most significant bits (first 8 bytes) in big endian
            for (int i = 0; i < 8; i++)
            {
                target[i] = (byte)(mostSigBits >> (56 - 8 * i));
            }

            // Write least significant bits (next 8 bytes) in big endian
            for (int i = 0; i < 8; i++)
            {
                target[8 + i] = (byte)(leastSigBits >> (56 - 8 * i));
            }
        }


        /// <returns>The length in bytes of the int as a VarInt</returns>
        public static int GetVarIntLength(int value)
        {
            int length = 1;

            while (!((value & 0xFFFFFF80) == 0))
            {
                ++length;
                value >>>= 7;
            }
            return length;
        }

        public static int WriteString(Span<byte> buffer, ReadOnlySpan<char> chars)
        {
            int strLength = Encoding.UTF8.GetByteCount(chars);
            int strLengthLength = WriteVarInt(buffer, 0, strLength);
            Encoding.UTF8.GetBytes(chars, buffer[strLengthLength..]);
            return strLengthLength + strLength;
        }
        

        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadAngle()
        {
            return Data[CursorPosition++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            return Data[CursorPosition++] == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return Data[CursorPosition++];
        }

        // Should be used or copied elsewhere before Dispose is called
        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            var bytes = Data.AsSpan(CursorPosition, count);
            CursorPosition += count;
            return bytes;
        }

        public double ReadDouble()
        {
            var value = BinaryPrimitives.ReadDoubleBigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 8;
            return value;
        }

        public byte[] ReadEntityMetadata()
        {
            throw new NotImplementedException();
        }

        public unsafe float ReadFloat()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 4;
            return *(float*)&value;
        }

        public string ReadIdentifier()
        {
            return ReadBytes(ReadVarInt()).ToString();
        }

        public int ReadInt()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 4;
            return value;
        }

        public long ReadLong()
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 8;
            return value;
        }

        public byte[] ReadNBTTag()
        {
            throw new NotImplementedException();
        }

        public Position ReadPosition()
        {
            var value = BinaryPrimitives.ReadUInt64BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 8;
            return new Position(value);
        }

        public short ReadShort()
        {
            var value = BinaryPrimitives.ReadInt16BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 2;
            return value;
        }

        public byte[] ReadSlot()
        {
            throw new NotImplementedException();
        }
        
        public string ReadString(int maxLength = short.MaxValue)
        {
            // Max string length is short.MaxValue with up to 4 bytes encoding each char
            int byteLengh = ReadVarInt();
            if (byteLengh > maxLength * 4)
                throw new InvalidDataException("String exceeded max encoded length");
            
            string str = Encoding.UTF8.GetString(ReadBytes(byteLengh));
            if (str.Length > maxLength)
                throw new InvalidDataException("String exceeded max length");

            return str;
        }

        public ushort ReadUShort()
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(
                Data.AsSpan(CursorPosition));
            CursorPosition += 2;
            return value;
        }

        public Guid ReadUUID() // Casts to .NET's Guid wrong, ToString won't work. Will fix when redoing stuff later
        {
            var value = new Guid(Data.AsSpan(CursorPosition, 16));
            CursorPosition += 16;
            return value;
        }

        public int ReadVarInt()
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;
            const int MAX_VARINT_LENGTH = 32;
            int value = 0;
            int length = 0;
            int currentByte;

            while (true)
            {
                currentByte = Data[CursorPosition++];
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        public int ReadVarInt(out int read)
        {
            const int SEGMENT_MASK = 0x7F;
            const int CONTINUE_MASK = 0x80;
            const int MAX_VARINT_LENGTH = 32;
            int value = 0;
            int length = 0;
            read = 0;
            int currentByte;

            while (true)
            {
                currentByte = Data[CursorPosition++];
                ++read;
                value |= (currentByte & SEGMENT_MASK) << length;
                if ((currentByte & CONTINUE_MASK) == 0) return value;
                length += 7;
                if (length >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
            }
        }

        public long ReadVarLong()
        {
            throw new NotImplementedException();
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
                ArrayPool<byte>.Shared.Return(Data);
            }
            _disposed = true;
        }
    }
}
