namespace MCGateway.Protocol;

public readonly record struct PlayerListItemProperty_V47(string Name, string Value, string? Signature);

public readonly record struct PlayerListItemData_V47
{
    public required Guid UUID { get; init; }
    public string? Name { get; init; }
    public PlayerListItemProperty_V47[]? Properties { get; init; }
    public int? Gamemode { get; init; }
    public int? Ping { get; init; }
    public string? DisplayName { get; init; } // JSON Chat
}

public readonly ref struct ChunkData_V47
{
    public int ChunkX { get; }
    public int ChunkZ { get; }
    public bool GroundUpContinuous { get; }
    public ushort PrimaryBitMask { get; }
    public ReadOnlySpan<byte> Data { get; }

    public ChunkData_V47(int chunkX, int chunkZ, bool groundUpContinuous, ushort primaryBitMask, ReadOnlySpan<byte> data)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        GroundUpContinuous = groundUpContinuous;
        PrimaryBitMask = primaryBitMask;
        Data = data;
    }
}

public readonly record struct ChunkColumnMetadata_V47(int ChunkX, int ChunkZ, ushort PrimaryBitMask);

public readonly ref struct MapChunkBulkData_V47
{
    public bool SkyLightSent { get; }
    public ChunkColumnMetadata_V47[] Metadata { get; }
    public ReadOnlySpan<byte> Data { get; }

    public MapChunkBulkData_V47(bool skyLightSent, ChunkColumnMetadata_V47[] metadata, ReadOnlySpan<byte> data)
    {
        SkyLightSent = skyLightSent;
        Metadata = metadata;
        Data = data;
    }
}

public readonly record struct Position_V47(int X, int Y, int Z);
public readonly record struct Rotation_V47(float Pitch, float Yaw, float Roll);
public readonly record struct Slot_V47(short BlockID, byte? ItemCount, short? ItemDamage);

public readonly record struct EntityMetadataEntry_V47(byte Index, byte Type, object Value)
{
    private void CheckType(byte expectedType)
    {
        if (Type != expectedType)
            throw new InvalidCastException($"Invalid metadata type. Expected {expectedType}, but was {Type}.");
    }

    public byte AsByte()
    {
        CheckType(0);
        return (byte)Value;
    }

    public short AsShort()
    {
        CheckType(1);
        return (short)Value;
    }

    public int AsInt()
    {
        CheckType(2);
        return (int)Value;
    }

    public float AsFloat()
    {
        CheckType(3);
        return (float)Value;
    }

    public string AsString()
    {
        CheckType(4);
        return (string)Value;
    }

    public Slot_V47 AsSlot()
    {
        CheckType(5);
        return (Slot_V47)Value;
    }

    public Position_V47 AsPosition()
    {
        CheckType(6);
        return (Position_V47)Value;
    }

    public Rotation_V47 AsRotation()
    {
        CheckType(7);
        return (Rotation_V47)Value;
    }
}

public readonly record struct EntityPropertyModifier_V47(Guid UUID, double Amount, sbyte Operation);
public readonly record struct EntityProperty_V47(string Key, double Value, EntityPropertyModifier_V47[] Modifiers);

public readonly record struct Team_V47
{
    public required string TeamName { get; init; }
    public required byte Mode { get; init; }
    public string? TeamDisplayName { get; init; }
    public string? TeamPrefix { get; init; }
    public string? TeamSuffix { get; init; }
    public bool? FriendlyFire { get; init; }
    public bool? SeeFriendlyInvisibles { get; init; }
    public string? NameTagVisibility { get; init; }
    public byte? Color { get; init; }
    public string[]? Players { get; init; }
}

public abstract record WorldBorderAction_V47;
public sealed record WorldBorderSetSizeAction_V47(double Diameter) : WorldBorderAction_V47;
public sealed record WorldBorderLerpSizeAction_V47(double OldDiameter, double NewDiameter, long Speed) : WorldBorderAction_V47;
public sealed record WorldBorderSetCenterAction_V47(double X, double Z) : WorldBorderAction_V47;
public sealed record WorldBorderInitializeAction_V47(double X, double Z, double OldDiameter, double NewDiameter, long Speed, int PortalTeleportBoundary, int WarningTime, int WarningBlocks) : WorldBorderAction_V47;
public sealed record WorldBorderSetWarningTimeAction_V47(int WarningTime) : WorldBorderAction_V47;
public sealed record WorldBorderSetWarningBlocksAction_V47(int WarningBlocks) : WorldBorderAction_V47;

public readonly ref struct UpdateEntityNBTData_V47
{
    public int EntityID { get; }
    public ReadOnlySpan<byte> NbtData { get; }

    public UpdateEntityNBTData_V47(int entityId, ReadOnlySpan<byte> nbtData)
    {
        EntityID = entityId;
        NbtData = nbtData;
    }
}

public readonly record struct ExplosionRecord_V47(sbyte X, sbyte Y, sbyte Z);

public readonly ref struct ExplosionPacketData_V47
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float Radius { get; }
    public ReadOnlySpan<ExplosionRecord_V47> Records { get; }
    public float PlayerMotionX { get; }
    public float PlayerMotionY { get; }
    public float PlayerMotionZ { get; }

    public ExplosionPacketData_V47(float x, float y, float z, float radius, ReadOnlySpan<ExplosionRecord_V47> records, float playerMotionX, float playerMotionY, float playerMotionZ)
    {
        X = x;
        Y = y;
        Z = z;
        Radius = radius;
        Records = records;
        PlayerMotionX = playerMotionX;
        PlayerMotionY = playerMotionY;
        PlayerMotionZ = playerMotionZ;
    }
}

public readonly record struct SpawnPlayerData_V47(int EntityID, Guid PlayerUUID, double X, double Y, double Z, byte Yaw, byte Pitch, short CurrentItem, EntityMetadataEntry_V47[] Metadata);
public readonly record struct SpawnObjectData_V47(int EntityID, byte Type, double X, double Y, double Z, byte Pitch, byte Yaw, int Data, short? VelocityX, short? VelocityY, short? VelocityZ);
public readonly record struct SpawnMobData_V47(int EntityID, byte Type, double X, double Y, double Z, byte Yaw, byte Pitch, byte HeadPitch, short VelocityX, short VelocityY, short VelocityZ, EntityMetadataEntry_V47[] Metadata);

public readonly record struct ParticlePacketData_V47(int ParticleID, bool LongDistance, float X, float Y, float Z, float OffsetX, float OffsetY, float OffsetZ, float ParticleData, int ParticleCount, int[]? ExtraData);

public readonly record struct UpdateSignData_V47(Position_V47 Location, string[] Lines);

public readonly ref struct UpdateBlockEntityData_V47
{
    public Position_V47 Location { get; }
    public byte Action { get; }
    public ReadOnlySpan<byte> NbtData { get; }

    public UpdateBlockEntityData_V47(Position_V47 location, byte action, ReadOnlySpan<byte> nbtData)
    {
        Location = location;
        Action = action;
        NbtData = nbtData;
    }
}

public readonly record struct WindowItemsData_V47(byte WindowID, Slot_V47[] Items);

public readonly record struct ScoreboardObjectiveData_V47
{
    public required string ObjectiveName { get; init; }
    public required byte Mode { get; init; }
    public string? ObjectiveValue { get; init; }
    public string? Type { get; init; }
}

public readonly record struct UpdateScoreData_V47
{
    public required string ScoreName { get; init; }
    public required byte Action { get; init; }
    public required string ObjectiveName { get; init; }
    public int? Value { get; init; }
}

public abstract record CombatEvent_V47;
public sealed record EnterCombatEvent_V47 : CombatEvent_V47;
public sealed record EndCombatEvent_V47(int Duration, int EntityID) : CombatEvent_V47;
public sealed record EntityDeadEvent_V47(int PlayerID, int EntityID, string Message) : CombatEvent_V47;

public readonly record struct Statistic_V47(string Name, int Value);

public abstract record TitleAction_V47;
public sealed record TitleSetTitleAction_V47(string Text) : TitleAction_V47;
public sealed record TitleSetSubtitleAction_V47(string Text) : TitleAction_V47;
public sealed record TitleSetTimesAction_V47(int FadeIn, int Stay, int FadeOut) : TitleAction_V47;
public sealed record TitleHideAction_V47 : TitleAction_V47;
public sealed record TitleResetAction_V47 : TitleAction_V47;

public readonly record struct MapIcon_V47(byte Direction, byte Type, byte X, byte Z);

public readonly ref struct MapData_V47
{
    public int MapId { get; }
    public byte Scale { get; }
    public MapIcon_V47[] Icons { get; }
    public byte Columns { get; }
    public byte? Rows { get; }
    public byte? X { get; }
    public byte? Z { get; }
    public ReadOnlySpan<byte> Data { get; }

    public MapData_V47(int mapId, byte scale, MapIcon_V47[] icons, byte columns, byte? rows, byte? x, byte? z, ReadOnlySpan<byte> data)
    {
        MapId = mapId;
        Scale = scale;
        Icons = icons;
        Columns = columns;
        Rows = rows;
        X = x;
        Z = z;
        Data = data;
    }
}

public readonly record struct PlayerDiggingData_V47(byte Status, Position_V47 Location, byte Face);
public readonly record struct PlayerBlockPlacementData_V47(Position_V47 Location, byte Direction, Slot_V47 HeldItem, byte CursorX, byte CursorY, byte CursorZ);
public readonly record struct ClickWindowData_V47(byte WindowID, short SlotIndex, byte Button, short ActionNumber, int Mode, Slot_V47 ClickedItem);
public readonly record struct CreativeInventoryActionData_V47(short SlotIndex, Slot_V47 ClickedItem);
public readonly record struct TabCompleteData_V47(string Text, Position_V47? LookedAtBlock);