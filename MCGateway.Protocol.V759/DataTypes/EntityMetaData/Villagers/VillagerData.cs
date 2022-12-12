namespace MCGateway.Protocol.V759.DataTypes.EntityMetaData.Villagers
{
    public struct VillagerData
    {
        public VillagerType Type { get; set; }
        public VillagerProfession Profession { get; set; }
        public byte Level { get; set; }

        public VillagerData(VillagerType type, VillagerProfession profession, byte level)
        {
            Type = type;
            Profession = profession;
            Level = level;
        }

        public string GetVillagerTypeName()
        {
            return "minecraft:" + Type.ToString();
        }

        public string GetVillagerProfessionName()
        {
            return "minecraft:" + Profession.ToString();
        }
    }
}
