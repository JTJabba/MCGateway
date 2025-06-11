using System.Buffers.Binary;

namespace MCGateway.Protocol;

public static partial class PacketReader
{
    public static void Read_V47_Clientbound_Id40_PlayDisconnect(
        ReadOnlySpan<byte> data,
        out string reason)
    {
        reason = Packet.ReadString(data, out _);
    }
    
    public static void Read_V47_Clientbound_Id41_ServerDifficulty(
        ReadOnlySpan<byte> data,
        out byte difficulty)
    {
        difficulty = data[0];
    }

    public static void Read_V47_Clientbound_Id42_CombatEvent(
        ReadOnlySpan<byte> data,
        out CombatEvent_V47 combatEvent)
    {
        int offset = 0;
        int eventId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        switch (eventId)
        {
            case 0:
                combatEvent = new EnterCombatEvent_V47();
                break;
            case 1:
            {
                int duration = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
                offset += bytesRead;
                int entityId = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
                combatEvent = new EndCombatEvent_V47(duration, entityId);
                break;
            }
            case 2:
            {
                int playerId = Packet.ReadVarIntWithLength(data.Slice(offset), out bytesRead);
                offset += bytesRead;
                int entityId = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
                offset += 4;
                string message = Packet.ReadString(data.Slice(offset), out _);
                combatEvent = new EntityDeadEvent_V47(playerId, entityId, message);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(eventId), "Invalid Combat Event ID");
        }
    }

    public static void Read_V47_Clientbound_Id43_Camera(
        ReadOnlySpan<byte> data,
        out int cameraId)
    {
        cameraId = Packet.ReadVarInt(data);
    }

    public static void Read_V47_Clientbound_Id44_WorldBorder(
        ReadOnlySpan<byte> data,
        out WorldBorderAction_V47 action)
    {
        int offset = 0;
        int actionId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        action = actionId switch
        {
            0 => new WorldBorderSetSizeAction_V47(BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset))),
            1 => new WorldBorderLerpSizeAction_V47(
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset)),
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset + 8)),
                ReadVarLong(data.Slice(offset + 16), out _)
            ),
            2 => new WorldBorderSetCenterAction_V47(
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset)),
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset + 8))
            ),
            3 => new WorldBorderInitializeAction_V47(
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset)),
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset + 8)),
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset + 16)),
                BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset + 24)),
                ReadVarLong(data.Slice(offset + 32), out bytesRead),
                Packet.ReadVarIntWithLength(data.Slice(offset + 32 + bytesRead), out int portalBytes),
                Packet.ReadVarIntWithLength(data.Slice(offset + 32 + bytesRead + portalBytes), out int warningTimeBytes),
                Packet.ReadVarInt(data.Slice(offset + 32 + bytesRead + portalBytes + warningTimeBytes))
            ),
            4 => new WorldBorderSetWarningTimeAction_V47(Packet.ReadVarInt(data.Slice(offset))),
            5 => new WorldBorderSetWarningBlocksAction_V47(Packet.ReadVarInt(data.Slice(offset))),
            _ => throw new ArgumentOutOfRangeException(nameof(actionId), "Invalid World Border action")
        };
    }

    public static void Read_V47_Clientbound_Id45_Title(
        ReadOnlySpan<byte> data,
        out TitleAction_V47 action)
    {
        int offset = 0;
        int actionId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        action = actionId switch
        {
            0 => new TitleSetTitleAction_V47(Packet.ReadString(data.Slice(offset), out _)),
            1 => new TitleSetSubtitleAction_V47(Packet.ReadString(data.Slice(offset), out _)),
            2 => new TitleSetTimesAction_V47(
                BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset)),
                BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset + 4)),
                BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset + 8))
            ),
            3 => new TitleHideAction_V47(),
            4 => new TitleResetAction_V47(),
            _ => throw new ArgumentOutOfRangeException(nameof(actionId), "Invalid Title action")
        };
    }

    public static void Read_V47_Clientbound_Id46_PlaySetCompression(
        ReadOnlySpan<byte> data,
        out int threshold)
    {
        threshold = Packet.ReadVarInt(data);
    }

    public static void Read_V47_Clientbound_Id47_PlayerListHeaderAndFooter(
        ReadOnlySpan<byte> data,
        bool wantHeader, out string header,
        bool wantFooter, out string footer)
    {
        header = "";
        footer = "";
        int offset = 0;
        
        string tempHeader = Packet.ReadString(data, out int bytesRead);
        if (wantHeader) header = tempHeader;
        offset += bytesRead;
        
        if (wantFooter) footer = Packet.ReadString(data.Slice(offset), out _);
    }
    
    public static void Read_V47_Clientbound_Id48_ResourcePackSend(
        ReadOnlySpan<byte> data,
        bool wantUrl, out string url,
        bool wantHash, out string hash)
    {
        url = "";
        hash = "";
        int offset = 0;

        string tempUrl = Packet.ReadString(data, out int bytesRead);
        if (wantUrl) url = tempUrl;
        offset += bytesRead;

        if (wantHash) hash = Packet.ReadString(data.Slice(offset), out _);
    }

    public static void Read_V4T_Clientbound_Id49_UpdateEntityNBT(
        ReadOnlySpan<byte> data,
        out UpdateEntityNBTData_V47 nbtData)
    {
        int offset = 0;
        int entityId = Packet.ReadVarIntWithLength(data, out int bytesRead);
        offset += bytesRead;

        // The rest of the packet is the NBT data.
        var nbtSpan = data.Slice(offset);
        nbtData = new UpdateEntityNBTData_V47(entityId, nbtSpan);
    }
}