using Google.Protobuf.Collections;
using Grpc.Core;
using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.InterceptionServices.Services;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.ChatService
{
    internal class ClientChatMessagerService(ConnectionsDictionary connectionsDictionary) : ClientChatMessager.ClientChatMessagerBase
    {
        private readonly ConnectionsDictionary _connectionsDictionary = connectionsDictionary;

        public override Task<ClientChatMessageConfirmation> ReceiveMessage(ClientChatMessageRequest request, ServerCallContext context)
        {
            RepeatedField<string> unformatted_uuids = request.Uuids;
              
            foreach (string uuid in unformatted_uuids)
            {
                Guid userId = Guid.Parse(uuid);
                GatewayConnection? connection; 
                if(!_connectionsDictionary.TryGetValue(userId, out connection))
                {
                    return Task.FromResult(new ClientChatMessageConfirmation()
                    {
                        Status = "Error in uuid " + uuid
                    });
                }
                // Get the IMCClientConnection, a version-agnostic abstraction of a Minecraft connection
                var genericConnection = connection!.ClientConnection;

                // Check if it implements a receiver interface imported from a specific version library
                if (genericConnection is not IClientboundReceiver clientConnection)
                {

                    return Task.FromResult(new ClientChatMessageConfirmation()
                    {
                        Status = "Error in uuid " + uuid
                    });
             
                }

                Packet builtPacket = BuildChatPacket(request.Message);

                clientConnection.Forward(builtPacket); 

            }

            return Task.FromResult(new ClientChatMessageConfirmation()
            {
                Status = "received"
            });
        }

        //not happy with this. Probs want to make building packets alot better.
        private Packet BuildChatPacket(string message, string color = "white")
        {
            var packetId = 0x5F;
            var packetIdLength = 1;
            
            var msgText = $"{{\"text\":\"{message}\",\"color\":\"{color.Trim().ToLowerInvariant()}\"}}";
            var msgLength = Encoding.UTF8.GetByteCount(msgText);
            var msgLengthLength = Packet.GetVarIntLength(msgLength);
            var packetLength = packetIdLength + msgLengthLength + msgLength + 1;
            var uncompressedEndOffset = Packet.SCRATCHSPACE + packetLength;
            var data = ArrayPool<byte>.Shared.Rent(uncompressedEndOffset);
            Packet.WriteVarInt(data.AsSpan(Packet.SCRATCHSPACE), packetId);
            var msgLenghOffset = Packet.SCRATCHSPACE + packetIdLength;
            Packet.WriteVarInt(data.AsSpan(msgLenghOffset), msgLength);
            var msgOffset = msgLenghOffset + msgLengthLength;
            Encoding.UTF8.GetBytes(msgText, data.AsSpan(msgOffset));
            var boolOffset = msgOffset + msgLength;
            data[boolOffset] = 0;

            return new Packet(
                            data,
                            uncompressedEndOffset,
                            0,
                            packetId,
                            packetIdLength);
        }

    }
   
}
