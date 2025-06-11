using System.Buffers.Binary;

namespace MCGateway.Protocol;

public static partial class PacketReader
{
    private static Guid ReadGuidBigEndian(ReadOnlySpan<byte> data)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        data[..16].CopyTo(guidBytes);
        // .NET Guid constructor expects the first three components in little-endian.
        // The Minecraft protocol sends the entire UUID in big-endian.
        // We need to swap the byte order for the first three components.
        (guidBytes[0], guidBytes[3]) = (guidBytes[3], guidBytes[0]);
        (guidBytes[1], guidBytes[2]) = (guidBytes[2], guidBytes[1]);
        (guidBytes[4], guidBytes[5]) = (guidBytes[5], guidBytes[4]);
        (guidBytes[6], guidBytes[7]) = (guidBytes[7], guidBytes[6]);
        return new Guid(guidBytes);
    }

    private static int CountSetBits(ushort n)
    {
        int count = 0;
        while (n > 0)
        {
            n &= (ushort)(n - 1);
            count++;
        }
        return count;
    }

    private static Position_V47 ReadPosition_V47(ReadOnlySpan<byte> data)
        {
            long val = BinaryPrimitives.ReadInt64BigEndian(data);
            int x = (int)(val >> 38);
            int y = (int)((val >> 26) & 0xFFF);
            int z = (int)(val & 0x3FFFFFF);
            return new Position_V47(x, y, z);
        }

    private static Slot_V47 ReadSlot_V47(ReadOnlySpan<byte> data, ref int offset)
    {
        var blockID = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        if (blockID == -1)
        {
            return new Slot_V47(blockID, null, null);
        }
        var itemCount = data[offset++];
        var itemDamage = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        if (data[offset] != 0) // NBT TAG_End
        {
            // We need a full NBT parser to read the rest of this.
            // For now, we cannot continue parsing this packet.
            throw new NotImplementedException("Cannot read entity metadata with NBT data in a Slot yet.");
        }
        offset++; // Consume the TAG_End byte
        return new Slot_V47(blockID, itemCount, itemDamage);
    }

    private static int GetVarIntLength(int value)
    {
        int length = 1;
        while (!((value & 0xFFFFFF80) == 0))
        {
            ++length;
            value >>>= 7;
        }
        return length;
    }

    private static long ReadVarLong(ReadOnlySpan<byte> buffer, out int bytesRead)
    {
        const int SEGMENT_MASK = 0x7F;
        const int CONTINUE_MASK = 0x80;
        const int MAX_VARLONG_LENGTH = 64;
        long value = 0;
        int offset = 0;
        int length = 0;
        byte currentByte;

        while (true)
        {
            currentByte = buffer[offset++];
            value |= (long)(currentByte & SEGMENT_MASK) << length;
            if ((currentByte & CONTINUE_MASK) == 0)
            {
                bytesRead = offset;
                return value;
            }
            length += 7;
            if (length >= MAX_VARLONG_LENGTH) throw new InvalidDataException("VarLong too big");
        }
    }
} 