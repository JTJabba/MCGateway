namespace MCGateway.Protocol.V759.DataTypes
{
    public struct Position
    {
        public ulong EncodedPos = 0;

        public int X // First 26 bits
        {
            get
            {
                uint x = (uint)(EncodedPos >> 38);
                if (x >= 0x2000000) x |= 0xFC000000;
                return (int)x;
            }
            set
            {
                EncodedPos &= 0x3FFFFFFFFF;
                EncodedPos |= (ulong)value << 38;
            }
        }
        public int Z // Middle 26 bits
        {
            get
            {
                uint z = (uint)(EncodedPos >> 12) & 0x3FFFFFF;
                if (z >= 0x2000000) z |= 0xFC000000;
                return (int)z;
            }
            set
            {
                EncodedPos &= 0xFFFFFFC000000FFF;
                EncodedPos |= (ulong)(value & 0x3FFFFFF) << 12;
            }
        }
        public int Y // Last 12 bits
        {
            get
            {
                int y = (int)(EncodedPos & 0xFFF);
                if (y >= 0x800) y |= 0xF000;
                return y;
            }
            set
            {
                EncodedPos &= 0xFFFFFFFFFFFFF000;
                EncodedPos |= (ulong)value & 0xFFF;
            }
        }

        public Position(ulong encodedPos)
        {
            EncodedPos = encodedPos;
        }

        public Position(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }

        public void SetXYZ(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }
    }
}
