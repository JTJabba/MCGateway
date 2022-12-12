using System.Buffers;
using System.Data;
using System.Net.Sockets;
using System.Runtime.Versioning;
using JTJabba.EasyConfig;
using Microsoft.Extensions.Logging;

namespace MCGateway.Protocol.V759
{
    public sealed class MCServerConnection : MCConnection, IMCServerConnection, IServerBoundReceiver
    {
        private readonly ILogger _logger = GatewayLogging.CreateLogger<MCServerConnection>();
        private bool _loggedIn;
        private readonly IClientBoundReceiver _receiver;

        public override string Username { get; init; }
        public override Guid UUID { get; init; }
        public override Config.TranslationsObject ClientTranslation { get; set; }

        private static readonly byte[] HandshakeBytes;

        static MCServerConnection()
        {
            int protocolLength = Packet.GetVarIntLength(V759Constants.ProtocolVersion);
            HandshakeBytes = new byte[protocolLength + 13];
            HandshakeBytes[0] = (byte)(protocolLength + 12); // Packet length
            HandshakeBytes[1] = 0x00; // Packet id
            Packet.WriteVarInt(HandshakeBytes, 2, V759Constants.ProtocolVersion);
            Span<byte> buffer = stackalloc byte[11]
            {
                0x07, // Server address length
                0x47, 0x61, 0x74, 0x65, 0x77, 0x61, 0x79, // "Gateway"
                0x63, // Port
                0xDD,
                0x02 // Next state
            };
            buffer.CopyTo(HandshakeBytes.AsSpan(2 + protocolLength));
        }

        public MCServerConnection(TcpClient tcpClient, string username, Guid uuid,
            Config.TranslationsObject translation, IClientBoundReceiver receiver) : base(tcpClient, Config.BufferSizes.ClientBound)
        {
            _receiver = receiver;
            Username = username;
            UUID = uuid;
            ClientTranslation = translation;

            // Send handshake
            _stream.Write(HandshakeBytes);
            // Send login start
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(127);
                try
                {
                    int offset = 5;
                    buffer[offset++] = 0x00; // Packet id
                    offset += Packet.WriteString(buffer.AsSpan(offset), username); // Username
                    buffer[offset++] = 0x00; // Has sig data
                    //buffer[offset++] = 0x01; // Has player uuid
                    //UUID.TryWriteBytes(buffer.AsSpan(offset)); // Player uuid
                    //offset += 16;
                    int packetLength = offset - 5;
                    offset = 5 - Packet.GetVarIntLength(packetLength);
                    Packet.WriteVarInt(buffer, offset, packetLength);
                    _stream.Write(buffer, offset, packetLength + 5 - offset);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            // Monitor login packets till login success received.
            // Not supporting plugin requests for now, and they can't be passed to client not in login
            try
            {
                Span<byte> loginPluginResponseBuffer = stackalloc byte[7];
                do
                {
                    var packet = ReadPacket();
                    _logger.LogDebug("Server constructor recieved packet with id " + packet.PacketID);
                    try
                    {
                        int packetID = packet.ReadByte();

                        if (packetID == 0x02) // Login success
                        {
                            _logger.LogInformation("Server login success");
                            _loggedIn = true;
                            packet.Dispose();
                            return;
                        }

                        if (packetID == 0x03) // Set compression
                        {
                            _compressionThreshold = packet.ReadVarInt();
                            packet.Dispose();
                            continue;
                        }

                        if (packetID == 0x04) // Login plugin request
                        {
                            int messageID = packet.ReadVarInt();
                            int offset = 0;
                            loginPluginResponseBuffer[offset++] = 0x02; // Packet id
                            offset += Packet.WriteVarInt(loginPluginResponseBuffer, 0, messageID); // Write same message id
                            loginPluginResponseBuffer[offset++] = 0x00; // Successful bool
                            WritePacket(loginPluginResponseBuffer.Slice(0, offset));
                            packet.Dispose();
                            continue;
                        }

                        throw new DataException("Server constructor received packet with unexpected id of " + packetID);
                    }
                    catch (Exception e)
                    {
                        if (GatewayLogging.InDebug) _logger.LogWarning(e, "Exception during login process");
                        packet.Dispose();
                    }
                } while (true);

            }
            finally
            {
                if (GatewayConfig.RequireCompressedFormat)
                {
                    if (_compressionThreshold <= 0 && _loggedIn)
                        throw new DataException("GatewayConfig.RequireCompressedFormat is set to true. Backend servers must use compression");
                }
            }
        }

        [RequiresPreviewFeatures]
        public static MCServerConnection? GetLoggedInServerConnection(
                TcpClient tcpClient,
                string username,
                Guid uuid,
                Config.TranslationsObject translation,
                IClientBoundReceiver reciever)
        {
            var con = new MCServerConnection(
                tcpClient, username, uuid, translation, reciever);
            return con._loggedIn ? con : null;
        }

        public void Forward(Packet packet)
        {
            try
            {
                WritePacket(packet.LengthPrefixedPacketBytes);
            }
            finally
            {
                packet.Dispose();
            }
        }

        [RequiresPreviewFeatures]
        public Task ReceiveTilClosed()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    var packet = ReadPacket();
                    //_logger.LogInformation("Received packet from server with ID {id}", packet.PacketID.ToString("X"));
                    _receiver.Forward(packet);
                }
            });
        }


        [RequiresPreviewFeatures]
        protected override void Dispose(bool disposing)
        {
            if (isDisposed) return;
            base.Dispose(disposing); // Dispose first so this can't be reentered
            if (disposing)
            {
                _receiver?.Dispose();
            }
        }
    }
}
