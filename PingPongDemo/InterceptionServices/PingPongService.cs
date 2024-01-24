using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using System.Buffers;
using System.Text;

namespace PingPongDemo.InterceptionServices
{
    public interface IPingPongService
    {
        public void PingReceived(Guid senderUuid);
    }

    internal sealed class PingPongService : IPingPongService
    {
        static class PongPacketFields
        {
            public static byte[] Data;
            public static int UncompressedEndOffset;
            public static int PacketId;
            public static int PacketIdLength;

            static PongPacketFields()
            {
                PacketId = 0x5F;
                PacketIdLength = 1;
                var msgText = $"{{\"text\":\"Pong from Gateway!\",\"color\":\"green\"}}";
                var msgLength = Encoding.UTF8.GetByteCount(msgText);
                var msgLengthLength = Packet.GetVarIntLength(msgLength);
                var packetLength = PacketIdLength + msgLengthLength + msgLength + 1;
                UncompressedEndOffset = Packet.SCRATCHSPACE + packetLength;
                Data = ArrayPool<byte>.Shared.Rent(UncompressedEndOffset);
                Packet.WriteVarInt(Data.AsSpan(Packet.SCRATCHSPACE), PacketId);
                var msgLenghOffset = Packet.SCRATCHSPACE + PacketIdLength;
                Packet.WriteVarInt(Data.AsSpan(msgLenghOffset), msgLength);
                var msgOffset = msgLenghOffset + msgLengthLength;
                Encoding.UTF8.GetBytes(msgText, Data.AsSpan(msgOffset));
                var boolOffset = msgOffset + msgLength;
                Data[boolOffset] = 0;
            }
        }

        readonly ILogger _logger = GatewayLogging.CreateLogger<PingPongService>();
        ConnectionsDictionary Connections;

        public PingPongService(ConnectionsDictionary connectionsDict)
        {
            Connections = connectionsDict;
        }

        public void PingReceived(Guid senderUuid)
        {
            //return;
            var gotCon = Connections.TryGetValue(senderUuid,
                out var gatewayCon);
            if (!gotCon) return;
            var genericCon = gatewayCon!.ClientConnection;
            if (genericCon is IClientboundReceiver clientCon)
            {
                clientCon.Forward(
                    new Packet(
                        PongPacketFields.Data,
                        PongPacketFields.UncompressedEndOffset,
                        0,
                        PongPacketFields.PacketId,
                        PongPacketFields.PacketIdLength));
                // Dont dispose bc we reuse array. Will eventually clean up and remove Dispose from packet
            }
            else
            {
                // Todo make stronger typed system for this
                _logger.LogError("Client on unsupported connection! Need to make stronger typed system for this!");
            }
        }
    }
}
