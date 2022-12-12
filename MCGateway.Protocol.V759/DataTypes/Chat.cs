using JTJabba.Utils;

namespace MCGateway.Protocol.V759.DataTypes
{
    public ref struct Chat
    {
        public UnsafeString str;

        public Chat(UnsafeString value)
        {
            str = value;
        }

        public void Dispose()
        {
            str.Dispose();
        }
    }
}
