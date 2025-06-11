using System;
using System.Buffers.Binary;

namespace MCGateway.Protocol;

public static partial class PacketReader
{
    public static void Read_V47_Serverbound_Id00_PlayKeepAlive(
        ReadOnlySpan<byte> data,
        out int keepAliveId)
    {
        keepAliveId = Packet.ReadVarInt(data);
    }
    
    public static void Read_V47_Serverbound_Id01_PlayChatMessage(
        ReadOnlySpan<byte> data,
        out string message)
    {
        message = Packet.ReadString(data, out _);
    }
    
    public static void Read_V47_Serverbound_Id02_UseEntity(
        ReadOnlySpan<byte> data,
        out int targetID,
        out int type)
    {
        targetID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        type = Packet.ReadVarInt(data.Slice(bytesRead));
    }
    
    public static void Read_V47_Serverbound_Id03_Player(
        ReadOnlySpan<byte> data,
        out bool onGround)
    {
        onGround = data[0] == 1;
    }
    
    public static void Read_V47_Serverbound_Id04_PlayerPosition(
        ReadOnlySpan<byte> data,
        out double x,
        out double feetY,
        out double z,
        out bool onGround)
    {
        int offset = 0;
        x = BinaryPrimitives.ReadDoubleBigEndian(data);
        offset += 8;
        feetY = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
        offset += 8;
        z = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
        offset += 8;
        onGround = data[offset] == 1;
    }
    
    public static void Read_V47_Serverbound_Id05_PlayerLook(
        ReadOnlySpan<byte> data,
        out float yaw,
        out float pitch,
        out bool onGround)
    {
        int offset = 0;
        yaw = BinaryPrimitives.ReadSingleBigEndian(data);
        offset += 4;
        pitch = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        onGround = data[offset] == 1;
    }
    
    public static void Read_V47_Serverbound_Id06_PlayerPositionAndLook(
        ReadOnlySpan<byte> data,
        out double x,
        out double y,
        out double z,
        out float yaw,
        out float pitch,
        out bool onGround)
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
        onGround = data[offset] == 1;
    }
    
    public static void Read_V47_Serverbound_Id07_PlayerDigging(
        ReadOnlySpan<byte> data,
        out PlayerDiggingData_V47 diggingData)
    {
        int offset = 0;
        byte status = data[offset++];
        var location = ReadPosition_V47(data.Slice(offset));
        offset += 8;
        byte face = data[offset];
        diggingData = new PlayerDiggingData_V47(status, location, face);
    }
    
    public static void Read_V47_Serverbound_Id08_PlayerBlockPlacement(
        ReadOnlySpan<byte> data,
        out PlayerBlockPlacementData_V47 placementData)
    {
        int offset = 0;
        var location = ReadPosition_V47(data);
        offset += 8;
        byte direction = data[offset++];
        var heldItem = ReadSlot_V47(data, ref offset);
        byte cursorX = data[offset++];
        byte cursorY = data[offset++];
        byte cursorZ = data[offset];
        placementData = new PlayerBlockPlacementData_V47(location, direction, heldItem, cursorX, cursorY, cursorZ);
    }
    
    public static void Read_V47_Serverbound_Id09_PlayHeldItemChange(
        ReadOnlySpan<byte> data,
        out short slot)
    {
        slot = BinaryPrimitives.ReadInt16BigEndian(data);
    }

    public static void Read_V47_Serverbound_Id0A_PlayAnimation(ReadOnlySpan<byte> data)
    {
        // No fields
    }

    public static void Read_V47_Serverbound_Id0B_EntityAction(
        ReadOnlySpan<byte> data,
        out int entityID,
        out int actionID,
        out int jumpBoost)
    {
        int offset = 0;
        entityID = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;
        
        actionID = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
        offset += bytesRead;
        
        jumpBoost = Packet.ReadVarInt(data.Slice(offset));
    }
    
    public static void Read_V47_Serverbound_Id0C_SteerVehicle(
        ReadOnlySpan<byte> data,
        out float sideways,
        out float forward,
        out byte flags)
    {
        int offset = 0;
        sideways = BinaryPrimitives.ReadSingleBigEndian(data);
        offset += 4;
        forward = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        flags = data[offset];
    }
    
    public static void Read_V47_Serverbound_Id0D_PlayCloseWindow(
        ReadOnlySpan<byte> data,
        out byte windowId)
    {
        windowId = data[0];
    }
    
    public static void Read_V47_Serverbound_Id0E_ClickWindow(
        ReadOnlySpan<byte> data,
        out ClickWindowData_V47 clickData)
    {
        int offset = 0;
        byte windowID = data[offset++];
        short slot = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        byte button = data[offset++];
        short actionNumber = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        int mode = Packet.ReadVarIntWithLength(data.Slice(offset), out int bytesRead);
        offset += bytesRead;
        var clickedItem = ReadSlot_V47(data, ref offset);
        clickData = new ClickWindowData_V47(windowID, slot, button, actionNumber, mode, clickedItem);
    }
    
    public static void Read_V47_Serverbound_Id0F_PlayConfirmTransaction(
        ReadOnlySpan<byte> data,
        out byte windowId,
        out short actionNumber,
        out bool accepted)
    {
        int offset = 0;
        windowId = data[offset++];
        actionNumber = BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));
        offset += 2;
        accepted = data[offset] == 1;
    }
    
    public static void Read_V47_Serverbound_Id10_CreativeInventoryAction(
        ReadOnlySpan<byte> data,
        out CreativeInventoryActionData_V47 actionData)
    {
        int offset = 0;
        short slot = BinaryPrimitives.ReadInt16BigEndian(data);
        offset += 2;
        var clickedItem = ReadSlot_V47(data, ref offset);
        actionData = new CreativeInventoryActionData_V47(slot, clickedItem);
    }
    
    public static void Read_V47_Serverbound_Id11_EnchantItem(
        ReadOnlySpan<byte> data,
        out byte windowId,
        out byte enchantment)
    {
        windowId = data[0];
        enchantment = data[1];
    }
    
    public static void Read_V47_Serverbound_Id12_PlayUpdateSign(
        ReadOnlySpan<byte> data,
        out UpdateSignData_V47 signData)
    {
        int offset = 0;
        var location = ReadPosition_V47(data);
        offset += 8;
        
        string[] lines = new string[4];
        for (int i = 0; i < 4; i++)
        {
            lines[i] = Packet.ReadString(data.Slice(offset), out int bytesRead);
            offset += bytesRead;
        }
        signData = new UpdateSignData_V47(location, lines);
    }
    
    public static void Read_V47_Serverbound_Id13_PlayPlayerAbilities(
        ReadOnlySpan<byte> data,
        out byte flags,
        out float flyingSpeed,
        out float walkingSpeed)
    {
        int offset = 0;
        flags = data[offset++];
        flyingSpeed = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
        offset += 4;
        walkingSpeed = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));
    }
    
    public static void Read_V47_Serverbound_Id14_PlayTabComplete(
        ReadOnlySpan<byte> data,
        out TabCompleteData_V47 tabCompleteData)
    {
        int offset = 0;
        string text = Packet.ReadString(data, out int bytesRead);
        offset += bytesRead;
        
        Position_V47? lookedAtBlock = null;
        if (data[offset++] == 1)
        {
            lookedAtBlock = ReadPosition_V47(data.Slice(offset));
        }
        tabCompleteData = new TabCompleteData_V47(text, lookedAtBlock);
    }
    
    public static void Read_V47_Serverbound_Id15_ClientSettings(
        ReadOnlySpan<byte> data,
        bool wantLocale, out string locale,
        out byte viewDistance,
        out byte chatMode,
        out bool chatColors,
        out byte displayedSkinParts)
    {
        locale = "";
        int offset = 0;
        
        if (wantLocale)
        {
            locale = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }
        else
        {
            _ = Packet.ReadString(data, out int bytesRead);
            offset += bytesRead;
        }
        
        viewDistance = data[offset++];
        chatMode = data[offset++];
        chatColors = data[offset++] == 1;
        displayedSkinParts = data[offset];
    }
    
    public static void Read_V47_Serverbound_Id16_ClientStatus(
        ReadOnlySpan<byte> data,
        out int actionId)
    {
        actionId = Packet.ReadVarInt(data);
    }
    
    public static void Read_V47_Serverbound_Id17_PlayPluginMessage(
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

    public static void Read_V47_Serverbound_Id18_Spectate(
        ReadOnlySpan<byte> data,
        out Guid targetPlayer)
    {
        targetPlayer = ReadGuidBigEndian(data);
    }

    public static void Read_V47_Serverbound_Id19_ResourcePackStatus(
        ReadOnlySpan<byte> data,
        bool wantHash, out string hash,
        out int result)
    {
        hash = "";
        int offset = 0;

        string tempHash = Packet.ReadString(data, out int bytesRead); // TODO only check length if wantHash = false
        if (wantHash) hash = tempHash;
        offset += bytesRead;

        result = Packet.ReadVarInt(data.Slice(offset));
    }
}