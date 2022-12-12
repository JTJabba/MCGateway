namespace MCGateway.Protocol.V759.DataTypes.EntityMetaData
{
    public struct Rotation
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Rotation(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public void SetXYZ(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }
    }
}
