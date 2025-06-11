using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace MCGateway.Protocol;

public static partial class PacketReader
{
    public static void Read_V47_Clientbound_Id20_EntityProperties(
        ReadOnlySpan<byte> data,
        out int entityID,
        out EntityProperty_V47[] properties)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        int propertyCount = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        
        properties = new EntityProperty_V47[propertyCount];
        for (int i = 0; i < propertyCount; i++)
        {
            string key = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            
            double value = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
            offset += 8;
            
            int modifierCount = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            
            var modifiers = new EntityPropertyModifier_V47[modifierCount];
            for (int j = 0; j < modifierCount; j++)
            {
                var uuid = ReadGuidBigEndian(data.Slice(offset));
                offset += 16;
                
                double amount = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
                offset += 8;
                
                sbyte operation = (sbyte)data[offset++];
                
                modifiers[j] = new EntityPropertyModifier_V47(uuid, amount, operation);
            }
            
            properties[i] = new EntityProperty_V47(key, value, modifiers);
        }
    }

    public static void Read_V47_Clientbound_Id21_ChunkData(
        ReadOnlySpan<byte> data,
        out ChunkData_V47 chunkData)
    {
        int offset = 0;
        
        int chunkX = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        
        int chunkZ = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        
        bool groundUp = data[offset++] == 1;
        
        ushort bitmask = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset));
        offset += 2;
        
        int dataSize = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        
        var chunkSpan = data.Slice(offset, dataSize);
        
        chunkData = new ChunkData_V47(chunkX, chunkZ, groundUp, bitmask, chunkSpan);
    }

    public static void Read_V47_Clientbound_Id22_MultiBlockChange(
        ReadOnlySpan<byte> data,
        out int chunkX,
        out int chunkZ,
        out int[] blockChanges)
    {
        int offset = 0;
        chunkX = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        chunkZ = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        int count = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        blockChanges = new int[count];
        for (int i = 0; i < count; i++)
        {
            blockChanges[i] = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
        }
    }

    public static void Read_V47_Clientbound_Id23_BlockChange(
        ReadOnlySpan<byte> data,
        out Position_V47 location,
        out int blockId)
    {
        int offset = 0;
        location = ReadPosition_V47(data);
        offset += 8;
        blockId = Packet.ReadVarInt(data.Slice(offset));
    }

    public static void Read_V47_Clientbound_Id24_BlockAction(
        ReadOnlySpan<byte> data,
        out Position_V47 location,
        out byte byte1,
        out byte byte2,
        out int blockType)
    {
        int offset = 0;
        location = ReadPosition_V47(data);
        offset += 8;
        byte1 = data[offset++];
        byte2 = data[offset++];
        blockType = Packet.ReadVarInt(data.Slice(offset));
    }

    public static void Read_V47_Clientbound_Id25_BlockBreakAnimation(
        ReadOnlySpan<byte> data,
        out int entityID,
        out long location,
        out byte destroyStage)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        location = BinaryPrimitives.ReadInt64BigEndian(data.Slice(offset));
        offset += 8;
        
        destroyStage = data[offset];
    }

    public static void Read_V47_Clientbound_Id26_MapChunkBulk(
        ReadOnlySpan<byte> data,
        out MapChunkBulkData_V47 chunkBulkData)
    {
        int offset = 0;

        bool skyLightSent = data[offset++] == 1;
        
        int chunkCount = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;

        var metadata = new ChunkColumnMetadata_V47[chunkCount];
        int totalDataSize = 0;

        for (int i = 0; i < chunkCount; i++)
        {
            int chunkX = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
            offset += 4;
            int chunkZ = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
            offset += 4;
            ushort bitmask = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset));
            offset += 2;
            
            metadata[i] = new ChunkColumnMetadata_V47(chunkX, chunkZ, bitmask);

            int sections = CountSetBits(bitmask);
            totalDataSize += sections * (8192 + 2048); // Block Data + Block Light
            if (skyLightSent)
            {
                totalDataSize += sections * 2048; // Sky Light
            }
            totalDataSize += 256; // Biomes
        }
        
        var chunkSpan = data.Slice(offset, totalDataSize);

        chunkBulkData = new MapChunkBulkData_V47(skyLightSent, metadata, chunkSpan);
    }

    public static void Read_V47_Clientbound_Id27_Explosion(
        ReadOnlySpan<byte> data,
        out ExplosionPacketData_V47 explosionData)
    {
        int offset = 0;
        float x = BinaryPrimitives.ReadSingleBigEndian(data);
        offset += 4;
        float y = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float z = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float radius = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;

        int recordCount = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;

        var records = MemoryMarshal.Cast<byte, ExplosionRecord_V47>(data.Slice(offset, recordCount * 3));
        offset += recordCount * 3;

        float playerMotionX = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float playerMotionY = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float playerMotionZ = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));

        explosionData = new ExplosionPacketData_V47(x, y, z, radius, records, playerMotionX, playerMotionY, playerMotionZ);
    }

    public static void Read_V47_Clientbound_Id28_Effect(
        ReadOnlySpan<byte> data,
        out int effectId,
        out Position_V47 position,
        out int effectData,
        out bool disableRelativeVolume)
    {
        int offset = 0;
        effectId = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        position = ReadPosition_V47(data.Slice(offset));
        offset += 8;
        effectData = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        disableRelativeVolume = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id29_SoundEffect(
        ReadOnlySpan<byte> data,
        bool wantSoundName, out string soundName,
        out int effectPositionX,
        out int effectPositionY,
        out int effectPositionZ,
        out float volume,
        out byte pitch)
    {
        soundName = "";
        int offset = 0;
        
        if (wantSoundName)
        {
            soundName = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }
        else
        {
            _ = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }
        
        effectPositionX = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        effectPositionY = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        effectPositionZ = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        volume = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        pitch = data[offset];
    }

    public static void Read_V47_Clientbound_Id2A_Particle(
        ReadOnlySpan<byte> data,
        out ParticlePacketData_V47 particleData)
    {
        int offset = 0;
        int particleId = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        bool longDistance = data[offset++] == 1;

        float x = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float y = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float z = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        
        float offsetX = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float offsetY = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        float offsetZ = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        
        float dataValue = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        
        int particleCount = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;

        int[]? extraData = null;
        if (particleId == 36 || particleId == 37 || particleId == 38) // ICON_CRACK, BLOCK_CRACK, BLOCK_DUST
        {
            int dataCount = (particleId == 36) ? 1 : 2; // ICON_CRACK has 1, BLOCK has 2
            extraData = new int[dataCount];
            for (int i = 0; i < dataCount; i++)
            {
                extraData[i] = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
                offset += bytesRead;
            }
        }
        
        particleData = new ParticlePacketData_V47(particleId, longDistance, x, y, z, offsetX, offsetY, offsetZ, dataValue, particleCount, extraData);
    }
    
    public static void Read_V47_Clientbound_Id2B_ChangeGameState(
        ReadOnlySpan<byte> data,
        out byte reason,
        out float value)
    {
        reason = data[0];
        value = BinaryPrimitives.ReadSingleBigEndian(data.Slice(1));
    }

    public static void Read_V47_Clientbound_Id2C_SpawnGlobalEntity(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte type,
        out int x,
        out int y,
        out int z)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        type = data[offset++];
        
        x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
    }
    
    public static void Read_V47_Clientbound_Id2D_OpenWindow(
        ReadOnlySpan<byte> data,
        out byte windowID,
        bool wantInventoryType, out string inventoryType,
        bool wantWindowTitle, out string windowTitle,
        out byte numberOfSlots)
    {
        inventoryType = "";
        windowTitle = "";
        int offset = 0;

        windowID = data[offset++];
        
        if (wantInventoryType)
        {
            inventoryType = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }
        else
        {
            _ = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }

        if (wantWindowTitle)
        {
            windowTitle = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }
        else
        {
            _ = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }
        
        numberOfSlots = data[offset];
    }
    
    public static void Read_V47_Clientbound_Id2E_PlayCloseWindow(
        ReadOnlySpan<byte> data,
        out byte windowId)
    {
        windowId = data[0];
    }
    
    public static void Read_V47_Clientbound_Id2F_SetSlot(
        ReadOnlySpan<byte> data,
        out byte windowId,
        out short slot,
        out Slot_V47 item)
    {
        int offset = 0;
        windowId = data[offset++];
        slot = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        item = ReadSlot_V47(data, ref offset);
    }
    
    public static void Read_V47_Clientbound_Id30_WindowItems(
        ReadOnlySpan<byte> data,
        out WindowItemsData_V47 windowItemsData)
    {
        int offset = 0;
        byte windowId = data[offset++];
        
        short count = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        
        var items = new Slot_V47[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = ReadSlot_V47(data, ref offset);
        }
        
        windowItemsData = new WindowItemsData_V47(windowId, items);
    }
    
    public static void Read_V47_Clientbound_Id31_WindowProperty(
        ReadOnlySpan<byte> data,
        out byte windowId,
        out short property,
        out short value)
    {
        windowId = data[0];
        property = BinaryPrimitives.ReadInt16BigEndian(data.Slice(1));
        value = BinaryPrimitives.ReadInt16BigEndian(data.Slice(3));
    }
    
    public static void Read_V47_Clientbound_Id32_PlayConfirmTransaction(
        ReadOnlySpan<byte> data,
        out byte windowId,
        out short actionNumber,
        out bool accepted)
    {
        windowId = data[0];
        actionNumber = BinaryPrimitives.ReadInt16BigEndian(data.Slice(1));
        accepted = data[3] == 1;
    }

    public static void Read_V47_Clientbound_Id33_PlayUpdateSign(
        ReadOnlySpan<byte> data,
        out UpdateSignData_V47 signData)
    {
        int offset = 0;
        var location = ReadPosition_V47(data);
        offset += 8;

        var lines = new string[4];
        for (int i = 0; i < 4; i++)
        {
            lines[i] = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }

        signData = new UpdateSignData_V47(location, lines);
    }

    public static void Read_V47_Clientbound_Id34_Map(
        ReadOnlySpan<byte> data,
        out MapData_V47 mapData)
    {
        int offset = 0;

        var mapId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        var scale = data[offset++];

        var iconCount = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;

        var icons = new MapIcon_V47[iconCount];
        for (int i = 0; i < iconCount; i++)
        {
            byte dirAndType = data[offset++];
            byte iconX = data[offset++];
            byte iconZ = data[offset++];
            byte direction = (byte)((dirAndType & 0xF0) >> 4);
            byte type = (byte)(dirAndType & 0x0F);
            icons[i] = new MapIcon_V47(direction, type, iconX, iconZ);
        }

        byte columns = data[offset++];
        byte? rows = null;
        byte? x = null;
        byte? z = null;
        ReadOnlySpan<byte> mapDataSpan = ReadOnlySpan<byte>.Empty;

        if (columns > 0)
        {
            rows = data[offset++];
            x = data[offset++];
            z = data[offset++];
            int dataLength = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            mapDataSpan = data.Slice(offset, dataLength);
        }
        
        mapData = new MapData_V47(mapId, scale, icons, columns, rows, x, z, mapDataSpan);
    }

    public static void Read_V47_Clientbound_Id35_UpdateBlockEntity(
        ReadOnlySpan<byte> data,
        out UpdateBlockEntityData_V47 blockEntityData)
    {
        int offset = 0;
        var location = ReadPosition_V47(data);
        offset += 8;

        byte action = data[offset++];
        
        // The rest of the packet is NBT data, which we cannot fully parse yet.
        // We will read it as a byte span. The wiki states it is gzipped.
        var nbtData = data.Slice(offset);

        blockEntityData = new UpdateBlockEntityData_V47(location, action, nbtData);
    }

    public static void Read_V47_Clientbound_Id36_OpenSignEditor(
        ReadOnlySpan<byte> data,
        out Position_V47 location)
    {
        location = ReadPosition_V47(data);
    }

    public static void Read_V47_Clientbound_Id37_Statistics(
        ReadOnlySpan<byte> data,
        out Statistic_V47[] statistics)
    {
        int offset = 0;
        int count = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        statistics = new Statistic_V47[count];
        for (int i = 0; i < count; i++)
        {
            string name = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            int value = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            statistics[i] = new Statistic_V47(name, value);
        }
    }

    public static void Read_V47_Clientbound_Id38_PlayerListItem(
        ReadOnlySpan<byte> data,
        out int action,
        out PlayerListItemData_V47[] players)
    {
        int offset = 0;
        action = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        int count = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;

        players = new PlayerListItemData_V47[count];

        for (int i = 0; i < count; i++)
        {
            var uuid = ReadGuidBigEndian(data.Slice(offset));
            offset += 16;

            players[i] = action switch
            {
                0 => ReadPlayerListItemAddPlayer(ref data, ref offset, uuid),
                1 => ReadPlayerListItemUpdateGamemode(ref data, ref offset, uuid),
                2 => ReadPlayerListItemUpdateLatency(ref data, ref offset, uuid),
                3 => ReadPlayerListItemUpdateDisplayName(ref data, ref offset, uuid),
                4 => new PlayerListItemData_V47 { UUID = uuid },
                _ => throw new ArgumentOutOfRangeException(nameof(action), "Invalid PlayerListItem action")
            };
        }
    }

    private static PlayerListItemData_V47 ReadPlayerListItemAddPlayer(ref ReadOnlySpan<byte> data, ref int offset, Guid uuid)
    {
        var name = Packet.ReadString(data.Slice(offset), out int bytesRead);
        offset += bytesRead;

        int propertyCount = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;
        var properties = new PlayerListItemProperty_V47[propertyCount];
        for (int j = 0; j < propertyCount; j++)
        {
            var propName = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            var propValue = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            string? propSignature = null;
            if (data[offset++] == 1)
            {
                propSignature = Packet.ReadString(data.Slice(offset), out bytesRead);
                offset += bytesRead;
            }
            properties[j] = new PlayerListItemProperty_V47(propName, propValue, propSignature);
        }

        var gamemode = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;
        var ping = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;
        string? displayName = null;
        if (data[offset++] == 1)
        {
            displayName = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
        }

        return new PlayerListItemData_V47
        {
            UUID = uuid,
            Name = name,
            Properties = properties,
            Gamemode = gamemode,
            Ping = ping,
            DisplayName = displayName
        };
    }

    private static PlayerListItemData_V47 ReadPlayerListItemUpdateGamemode(ref ReadOnlySpan<byte> data, ref int offset, Guid uuid)
    {
        var gamemode = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        return new PlayerListItemData_V47 { UUID = uuid, Gamemode = gamemode };
    }

    private static PlayerListItemData_V47 ReadPlayerListItemUpdateLatency(ref ReadOnlySpan<byte> data, ref int offset, Guid uuid)
    {
        var ping = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        return new PlayerListItemData_V47 { UUID = uuid, Ping = ping };
    }

    private static PlayerListItemData_V47 ReadPlayerListItemUpdateDisplayName(ref ReadOnlySpan<byte> data, ref int offset, Guid uuid)
    {
        string? displayName = null;
        if (data[offset++] == 1)
        {
            displayName = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }
        return new PlayerListItemData_V47 { UUID = uuid, DisplayName = displayName };
    }

    public static void Read_V47_Clientbound_Id39_PlayPlayerAbilities(
        ReadOnlySpan<byte> data,
        out sbyte flags,
        out float flyingSpeed,
        out float fovModifier)
    {
        flags = (sbyte)data[0];
        flyingSpeed = BinaryPrimitives.ReadSingleBigEndian(data.Slice(1));
        fovModifier = BinaryPrimitives.ReadSingleBigEndian(data.Slice(5));
    }

    public static void Read_V47_Clientbound_Id3A_PlayTabComplete(
        ReadOnlySpan<byte> data,
        out string[] matches)
    {
        int offset = 0;
        int count = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        matches = new string[count];
        for (int i = 0; i < count; i++)
        {
            matches[i] = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
        }
    }
    
    public static void Read_V47_Clientbound_Id3B_ScoreboardObjective(
        ReadOnlySpan<byte> data,
        out ScoreboardObjectiveData_V47 objectiveData)
    {
        int offset = 0;
        string objectiveName = Packet.ReadString(data, out int bytesRead);
        offset += bytesRead;

        byte mode = data[offset++];

        string? objectiveValue = null;
        string? type = null;

        if (mode == 0 || mode == 2)
        {
            objectiveValue = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            type = Packet.ReadString(data.Slice(offset), out _);
        }

        objectiveData = new ScoreboardObjectiveData_V47
        {
            ObjectiveName = objectiveName,
            Mode = mode,
            ObjectiveValue = objectiveValue,
            Type = type
        };
    }

    public static void Read_V47_Clientbound_Id3C_UpdateScore(
        ReadOnlySpan<byte> data,
        out UpdateScoreData_V47 scoreData)
    {
        int offset = 0;
        string scoreName = Packet.ReadString(data, out int bytesRead);
        offset += bytesRead;

        byte action = data[offset++];
        
        string objectiveName = Packet.ReadString(data.Slice(offset), out bytesRead);
        offset += bytesRead;

        int? value = null;
        if (action != 1) // If action is not "remove"
        {
            value = Packet.ReadVarInt(data.Slice(offset));
        }
        
        scoreData = new UpdateScoreData_V47
        {
            ScoreName = scoreName,
            Action = action,
            ObjectiveName = objectiveName,
            Value = value
        };
    }

    public static void Read_V47_Clientbound_Id3D_DisplayScoreboard(
        ReadOnlySpan<byte> data,
        out byte position,
        out string scoreName)
    {
        int offset = 0;
        position = data[offset++];
        scoreName = Packet.ReadString(data.Slice(offset), out _);
    }
    
    public static void Read_V47_Clientbound_Id3E_Teams(
        ReadOnlySpan<byte> data,
        out Team_V47 team)
    {
        int offset = 0;
        string teamName = Packet.ReadString(data, out int bytesRead);
        offset += bytesRead;

        byte mode = data[offset++];

        string? teamDisplayName = null;
        string? teamPrefix = null;
        string? teamSuffix = null;
        bool? friendlyFire = null;
        bool? seeFriendlyInvisibles = null;
        string? nameTagVisibility = null;
        byte? color = null;
        string[]? players = null;

        if (mode == 0 || mode == 2)
        {
            teamDisplayName = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            teamPrefix = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            teamSuffix = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            byte friendlyFireByte = data[offset++];
            friendlyFire = (friendlyFireByte & 0x01) != 0;
            seeFriendlyInvisibles = (friendlyFireByte & 0x02) != 0;
            nameTagVisibility = Packet.ReadString(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            color = data[offset++];
        }

        if (mode == 0 || mode == 3 || mode == 4)
        {
            int playerCount = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
            players = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                players[i] = Packet.ReadString(data.Slice(offset), out bytesRead);
                offset += bytesRead;
            }
        }
        
        team = new Team_V47
        {
            TeamName = teamName,
            Mode = mode,
            TeamDisplayName = teamDisplayName,
            TeamPrefix = teamPrefix,
            TeamSuffix = teamSuffix,
            FriendlyFire = friendlyFire,
            SeeFriendlyInvisibles = seeFriendlyInvisibles,
            NameTagVisibility = nameTagVisibility,
            Color = color,
            Players = players
        };
    }
    
    public static void Read_V47_Clientbound_Id3F_PlayPluginMessage(
        ReadOnlySpan<byte> data,
        bool wantChannel, out string channel,
        bool wantData, out ReadOnlySpan<byte> messageData)
    {
        channel = "";
        messageData = ReadOnlySpan<byte>.Empty;
        int offset = 0;
        
        string tempChannel = Packet.ReadString(data, out int bytesRead);
        if (wantChannel) channel = tempChannel;
        offset += bytesRead;
        
        if (wantData) messageData = data.Slice(offset);
    }
}