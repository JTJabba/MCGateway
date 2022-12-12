using JTJabba.Utils;
using MCGateway.Protocol.V759.DataTypes;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public ref struct Packet
    {
        public readonly int PacketLength { get; init; }
        public readonly int PacketID { get; init; }
        private readonly byte[] _packetBytes;

        public Span<byte> LengthPrefixedPacketBytes5Offset
        {
            get => _packetBytes.AsSpan(0, PacketLength + 5);
        }
        /// <summary>
        /// CursorPosition is initialized to zero, before <c>PacketID</c>. Use MoveCursor to adjust cursor (Compatibility in using blocks)
        /// </summary>
        public int CursorPosition { get; private set; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveCursor(int x) => CursorPosition += x;

        /// <summary>
        /// Initializes packet with CursorPosition after packetID
        /// <c>byte[] packetBytes</c> MUST be rented from ArrayPool Shared, and contain packetLength, packetID and data.
        /// All packets need to be disposed.
        /// </summary>
        public Packet(byte[] lengthPrefixedPacketBytes5Offset)
        {
            int offset = 5;
            PacketLength = ReadVarInt(lengthPrefixedPacketBytes5Offset, ref offset);
            PacketID = ReadVarInt(lengthPrefixedPacketBytes5Offset, ref offset);
            CursorPosition = offset;
            _packetBytes = lengthPrefixedPacketBytes5Offset;
        }

        /// <summary>
        /// Initializes packet with CursorPosition at dataOffset
        /// <c>byte[] packetBytes</c> MUST be rented from ArrayPool Shared, and contain packetLength, packetID and data.
        /// All packets need to be disposed.
        /// </summary>
        public Packet(byte[] lengthPrefixedPacketBytes5Offset, int packetLength, int packetID, int headerLength)
        {
            PacketLength = packetLength;
            PacketID = packetID;
            CursorPosition = headerLength + 5;
            _packetBytes = lengthPrefixedPacketBytes5Offset;
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
            return _packetBytes[CursorPosition++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            return _packetBytes[CursorPosition++] == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return _packetBytes[CursorPosition++];
        }

        // Should be used or copied elsewhere before Dispose is called
        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            var bytes = _packetBytes.AsSpan(CursorPosition, count);
            CursorPosition += count;
            return bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Chat ReadChat(int maxLength = 262_144)
        {
            return new Chat(ReadUnsafeString(maxLength));
        }

        public double ReadDouble()
        {
            var value = BinaryPrimitives.ReadDoubleBigEndian(
                _packetBytes.AsSpan(CursorPosition));
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
                _packetBytes.AsSpan(CursorPosition));
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
                _packetBytes.AsSpan(CursorPosition));
            CursorPosition += 4;
            return value;
        }

        public long ReadLong()
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(
                _packetBytes.AsSpan(CursorPosition));
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
                _packetBytes.AsSpan(CursorPosition));
            CursorPosition += 8;
            return new Position(value);
        }

        public short ReadShort()
        {
            var value = BinaryPrimitives.ReadInt16BigEndian(
                _packetBytes.AsSpan(CursorPosition));
            CursorPosition += 2;
            return value;
        }

        public byte[] ReadSlot()
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// If possible use ReadUnsafeString
        /// </summary>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
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

        public UnsafeString ReadUnsafeString(int maxLength = short.MaxValue)
        {
            // Max string length is short.MaxValue with up to 4 bytes encoding each char
            int byteLengh = ReadVarInt();
            if (byteLengh > maxLength * 4)
                throw new InvalidDataException("String exceeded max encoded length");

            UnsafeString str = UnsafeString.GetFromBytes(ReadBytes(byteLengh));
            if (str.Length > maxLength)
                throw new InvalidDataException("String exceeded max length");

            return str;
        }

        public ushort ReadUShort()
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(
                _packetBytes.AsSpan(CursorPosition));
            CursorPosition += 2;
            return value;
        }

        public Guid ReadUUID()
        {
            var value = new Guid(_packetBytes.AsSpan(CursorPosition));
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
                currentByte = _packetBytes[CursorPosition++];
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

        /// <summary>
        /// This method must only be called once
        /// </summary>
        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_packetBytes);
        }
    }
}
