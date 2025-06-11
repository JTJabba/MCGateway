using System.Buffers.Binary;

namespace MCGateway.Protocol;

public static partial class PacketReader
{
    public static void Read_V47_Clientbound_Id00_PlayKeepAlive(
        ReadOnlySpan<byte> data,
        out int keepAliveId)
    {
        keepAliveId = Packet.ReadVarInt(data);
    }

    public static void Read_V47_Clientbound_Id01_JoinGame(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte gamemode,
        out int dimension,
        out byte difficulty,
        out byte maxPlayers,
        out string levelType,
        out bool reducedDebugInfo)
    {
        int offset = 0;
        entityID = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        gamemode = data[offset++];
        dimension = data[offset++];
        difficulty = data[offset++];
        maxPlayers = data[offset++];
        levelType = Packet.ReadString(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        reducedDebugInfo = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id02_PlayChatMessage(
        ReadOnlySpan<byte> data,
        bool wantJsonData, out string jsonData,
        out byte position)
    {
        jsonData = "";
        int offset = 0;

        if (wantJsonData)
        {
            jsonData = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }
        else
        {
            // TODO just check length of string if field not needed
            _ = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }

        position = data[offset];
    }

    public static void Read_V47_Clientbound_Id03_TimeUpdate(
        ReadOnlySpan<byte> data,
        out long worldAge,
        out long timeOfDay)
    {
        worldAge = BinaryPrimitives.ReadInt64BigEndian(data);
        timeOfDay = BinaryPrimitives.ReadInt64BigEndian(data.Slice(8));
    }

    public static void Read_V47_Clientbound_Id04_EntityEquipment(
        ReadOnlySpan<byte> data,
        out int entityId,
        out short slot,
        out Slot_V47 item)
    {
        int offset = 0;
        entityId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        slot = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        item = ReadSlot_V47(data, ref offset);
    }

    public static void Read_V47_Clientbound_Id05_SpawnPosition(
        ReadOnlySpan<byte> data,
        out Position_V47 position)
    {
        position = ReadPosition_V47(data);
    }

    public static void Read_V47_Clientbound_Id06_UpdateHealth(
        ReadOnlySpan<byte> data,
        out float health,
        out int food,
        out float foodSaturation)
    {
        int offset = 0;
        health = BinaryPrimitives.ReadSingleBigEndian(data);
        offset += 4;
        food = Packet.ReadVarIntWithLength(data.Slice(offset), out int foodBytes);
        offset += foodBytes;
        foodSaturation = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
    }

    public static void Read_V47_Clientbound_Id07_Respawn(
        ReadOnlySpan<byte> data,
        out int dimension,
        out byte difficulty,
        out byte gamemode,
        bool wantLevelType, out string levelType)
    {
        levelType = "";
        int offset = 0;
        dimension = BinaryPrimitives.ReadInt32BigEndian(data);
        offset += 4;
        difficulty = data[offset++];
        gamemode = data[offset++];
        if (wantLevelType)
        {
            levelType = Packet.ReadString(data.Slice(offset), out _);
        }
    }

    public static void Read_V47_Clientbound_Id08_PlayerPositionAndLook(
        ReadOnlySpan<byte> data,
        out double x,
        out double y,
        out double z,
        out float yaw,
        out float pitch,
        out byte flags)
    {
        int offset = 0;
        x = BinaryPrimitives.ReadDoubleBigEndian(data);
        offset += 8;
        y = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
        offset += 8;
        z = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
        offset += 8;
        yaw = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        pitch = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        flags = data[offset];
    }

    public static void Read_V47_Clientbound_Id09_PlayHeldItemChange(
        ReadOnlySpan<byte> data,
        out byte slot)
    {
        slot = data[0];
    }

    public static void Read_V47_Clientbound_Id0A_UseBed(
        ReadOnlySpan<byte> data,
        out int entityID,
        out long location)
    {
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        location = BinaryPrimitives.ReadInt64BigEndian(data.Slice(bytesRead));
    }
    
    public static void Read_V47_Clientbound_Id0B_PlayAnimation(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte animation)
    {
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        animation = data[bytesRead];
    }
    
    public static void Read_V47_Clientbound_Id0C_SpawnPlayer(
        ReadOnlySpan<byte> data,
        out SpawnPlayerData_V47 playerData)
    {
        int offset = 0;
        int entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        Guid playerUUID = ReadGuidBigEndian(data.Slice(offset));
        offset += 16;
        
        double x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        
        byte yaw = data[offset++];
        byte pitch = data[offset++];
        
        short currentItem = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;

        Read_V47_Clientbound_Id1C_EntityMetadata(data.Slice(offset), out _, out var metadata);
        
        playerData = new SpawnPlayerData_V47(entityID, playerUUID, x, y, z, yaw, pitch, currentItem, metadata);
    }

    public static void Read_V47_Clientbound_Id0D_CollectItem(
        ReadOnlySpan<byte> data,
        out int collectedEntityID,
        out int collectorEntityID)
    {
        collectedEntityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        collectorEntityID = Packet.ReadVarInt(data.Slice(bytesRead));
    }

    public static void Read_V47_Clientbound_Id0E_SpawnObject(
        ReadOnlySpan<byte> data,
        out SpawnObjectData_V47 objectData)
    {
        int offset = 0;
        int entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        byte type = data[offset++];

        double x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        
        byte pitch = data[offset++];
        byte yaw = data[offset++];
        
        int packetData = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        
        short? velocityX = null;
        short? velocityY = null;
        short? velocityZ = null;
        
        if (packetData > 0)
        {
            velocityX = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
            offset += 2;
            velocityY = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
            offset += 2;
            velocityZ = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        }

        objectData = new SpawnObjectData_V47(entityID, type, x, y, z, pitch, yaw, packetData, velocityX, velocityY, velocityZ);
    }
    
    public static void Read_V47_Clientbound_Id0F_SpawnMob(
        ReadOnlySpan<byte> data,
        out SpawnMobData_V47 mobData)
    {
        int offset = 0;
        int entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        byte type = data[offset++];

        double x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        double z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)) / 32.0;
        offset += 4;
        
        byte yaw = data[offset++];
        byte pitch = data[offset++];
        byte headPitch = data[offset++];
        
        short velocityX = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        short velocityY = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        short velocityZ = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;

        Read_V47_Clientbound_Id1C_EntityMetadata(data.Slice(offset), out _, out var metadata);

        mobData = new SpawnMobData_V47(entityID, type, x, y, z, yaw, pitch, headPitch, velocityX, velocityY, velocityZ, metadata);
    }

    public static void Read_V47_Clientbound_Id10_SpawnPainting(
        ReadOnlySpan<byte> data,
        out int entityID,
        bool wantTitle, out string title,
        out long location,
        out byte direction)
    {
        title = "";
        int offset = 0;

        entityID = Packet.ReadVarIntWithLength(data, out int entityIdBytes);
        offset += entityIdBytes;

        if (wantTitle)
        {
            title = Packet.ReadString(data.Slice(offset), out int titleBytes);
            offset += titleBytes;
        }
        else
        {
            _ = Packet.ReadString(data.Slice(offset), out int titleBytes);
            offset += titleBytes;
        }

        location = BinaryPrimitives.ReadInt64BigEndian(data.Slice(offset));
        offset += 8;

        direction = data[offset];
    }
    
    public static void Read_V47_Clientbound_Id11_SpawnExperienceOrb(
        ReadOnlySpan<byte> data,
        out int entityID,
        out int x,
        out int y,
        out int z,
        out short count)
    {
        int offset = 0;
        
        entityID = Packet.ReadVarIntWithLength(data, out int entityIdBytes);
        offset += entityIdBytes;
        
        x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;

        y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;

        z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        
        count = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
    }

    public static void Read_V47_Clientbound_Id12_EntityVelocity(
        ReadOnlySpan<byte> data,
        out int entityID,
        out short velocityX,
        out short velocityY,
        out short velocityZ)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        velocityX = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        
        velocityY = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        
        velocityZ = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
    }

    public static void Read_V47_Clientbound_Id13_DestroyEntities(
        ReadOnlySpan<byte> data,
        out int[] entityIds)
    {
        int offset = 0;
        int count = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        entityIds = new int[count];
        for (int i = 0; i < count; i++)
        {
            entityIds[i] = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
            offset += bytesRead;
        }
    }

    public static void Read_V47_Clientbound_Id14_Entity(
        ReadOnlySpan<byte> data,
        out int entityId)
    {
        entityId = Packet.ReadVarInt(data);
    }

    public static void Read_V47_Clientbound_Id15_EntityRelativeMove(
        ReadOnlySpan<byte> data,
        out int entityID,
        out sbyte deltaX,
        out sbyte deltaY,
        out sbyte deltaZ,
        out bool onGround)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        deltaX = (sbyte)data[offset++];
        deltaY = (sbyte)data[offset++];
        deltaZ = (sbyte)data[offset++];
        onGround = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id16_EntityLook(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte yaw,
        out byte pitch,
        out bool onGround)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        yaw = data[offset++];
        pitch = data[offset++];
        onGround = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id17_EntityLookRelativeMove(
        ReadOnlySpan<byte> data,
        out int entityID,
        out sbyte deltaX,
        out sbyte deltaY,
        out sbyte deltaZ,
        out byte yaw,
        out byte pitch,
        out bool onGround)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        deltaX = (sbyte)data[offset++];
        deltaY = (sbyte)data[offset++];
        deltaZ = (sbyte)data[offset++];
        yaw = data[offset++];
        pitch = data[offset++];
        onGround = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id18_EntityTeleport(
        ReadOnlySpan<byte> data,
        out int entityID,
        out int x,
        out int y,
        out int z,
        out byte yaw,
        out byte pitch,
        out bool onGround)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        x = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        z = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        offset += 4;
        yaw = data[offset++];
        pitch = data[offset++];
        onGround = data[offset] == 1;
    }
    
    public static void Read_V47_Clientbound_Id19_EntityHeadLook(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte headYaw)
    {
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        headYaw = data[bytesRead];
    }
    
    public static void Read_V47_Clientbound_Id1A_EntityStatus(
        ReadOnlySpan<byte> data,
        out int entityId,
        out byte status)
    {
        entityId = BinaryPrimitives.ReadInt32BigEndian(data);
        status = data[4];
    }

    public static void Read_V47_Clientbound_Id1B_AttachEntity(
        ReadOnlySpan<byte> data,
        out int entityId,
        out int vehicleId,
        out bool leash)
    {
        entityId = BinaryPrimitives.ReadInt32BigEndian(data);
        vehicleId = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4));
        leash = data[8] == 1;
    }

    public static void Read_V47_Clientbound_Id1C_EntityMetadata(
        ReadOnlySpan<byte> data,
        out int entityID,
        out EntityMetadataEntry_V47[] metadata)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        var metadataList = new List<EntityMetadataEntry_V47>();
        while (true)
        {
            byte item = data[offset++];
            if (item == 0x7F) break;

            byte type = (byte)((item & 0xE0) >> 5);
            byte index = (byte)(item & 0x1F);

            object value;
            switch (type)
            {
                case 0: // byte
                    value = data[offset];
                    offset += 1;
                    break;
                case 1: // short
                    value = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
                    offset += 2;
                    break;
                case 2: // int
                    value = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
                    offset += 4;
                    break;
                case 3: // float
                    value = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
                    offset += 4;
                    break;
                case 4: // string
                    value = Packet.ReadString(data.Slice(offset), out bytesRead);
                    offset += bytesRead;
                    break;
                case 5: // Slot
                    value = ReadSlot_V47(data, ref offset);
                    break;
                case 6: // Position
                    value = new Position_V47(
                        BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)),
                        BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset + 4)),
                        BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset + 8))
                    );
                    offset += 12;
                    break;
                case 7: // Rotation
                    value = new Rotation_V47(
                        BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset)),
                        BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset + 4)),
                        BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset + 8))
                    );
                    offset += 12;
                    break;
                default:
                    throw new InvalidDataException("Invalid metadata type");
            }

            metadataList.Add(new EntityMetadataEntry_V47(index, type, value));
        }
        metadata = metadataList.ToArray();
    }
    
    public static void Read_V47_Clientbound_Id1D_EntityEffect(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte effectID,
        out byte amplifier,
        out int duration,
        out bool hideParticles)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        effectID = data[offset++];
        amplifier = data[offset++];
        
        duration = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;
        
        hideParticles = data[offset] == 1;
    }

    public static void Read_V47_Clientbound_Id1E_RemoveEntityEffect(
        ReadOnlySpan<byte> data,
        out int entityID,
        out byte effectID)
    {
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        effectID = data[bytesRead];
    }
    
    public static void Read_V47_Clientbound_Id1F_SetExperience(
        ReadOnlySpan<byte> data,
        out float experienceBar,
        out int level,
        out int totalExperience)
    {
        int offset = 4;
        experienceBar = BinaryPrimitives.ReadSingleBigEndian(data);
        
        level = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        
        totalExperience = Packet.ReadVarInt(data.Slice(offset));
    }
}