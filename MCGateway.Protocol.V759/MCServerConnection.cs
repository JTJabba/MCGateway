using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using JTJabba.EasyConfig;
using Microsoft.Extensions.Logging;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public sealed class MCServerConnection : MCConnection, IMCServerConnection, IServerBoundReceiver
    {
        readonly ILogger _logger = GatewayLogging.CreateLogger<MCServerConnection>();
        bool _loggedIn = false;
        readonly IClientBoundReceiver _receiver;

        static readonly byte[] HandshakeBytes;

        public override string Username { get; init; }
        public override Guid UUID { get; init; }
        public override Config.TranslationsObject ClientTranslation { get; set; }


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


        public MCServerConnection(
            TcpClient tcpClient, string username, Guid uuid, Config.TranslationsObject translation, IClientBoundReceiver receiver)
            : base(tcpClient, Config.BufferSizes.ClientBound, (ulong)DateTime.UtcNow.Ticks)
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
                Span<byte> loginPluginResponseBuffer = stackalloc byte[9];
                do
                {
                    using var packet = ReadPacketLogin();
                    try
                    {
                        if (packet.PacketID == 0x02) // Login success
                        {
                            _loggedIn = true;
                            return;
                        }

                        if (packet.PacketID == 0x03) // Set compression
                        {
                            _compressionThreshold = packet.ReadVarInt();
                            continue;
                        }

                        if (packet.PacketID == 0x04) // Login plugin request
                        {
                            int messageID = packet.ReadVarInt(out int responseLength);
                            responseLength += 1; // Add packet id
                            if (_compressionThreshold >= 0) ++responseLength; // If compressed format add slot for 0
                            int offset = _compressionThreshold >= 0 ? 2 : 1; // Determine packet id offset
                            loginPluginResponseBuffer[0] = (byte)responseLength; // Write packet length
                            loginPluginResponseBuffer[1] = 0; // Initialize to 0 incase we skip (compressed format)
                            loginPluginResponseBuffer[offset++] = 0x02; // Write packet id
                            Packet.WriteVarInt(loginPluginResponseBuffer, offset++, messageID); // Write message id
                            loginPluginResponseBuffer[offset] = 0x00; // Write successful bool (don't support plugins rn)

                            _stream.Write(loginPluginResponseBuffer.Slice(0, 1 + responseLength)); // Account for length prefix
                            continue;
                        }

                        throw new InvalidDataException("Server constructor received packet with unexpected id of " + packet.PacketID + "and number of 0x" + PacketsRead.ToString("X"));
                    }
                    catch (Exception ex)
                    {
                        if (GatewayLogging.InDebug) _logger.LogWarning(ex, "Exception during login process");
                        throw;
                    }
                    finally
                    {
                        packet.Dispose();
                    }
                } while (true);
            }
            finally
            {
                if (GatewayConfig.RequireCompressedFormat)
                {
                    if (_compressionThreshold <= 0 && _loggedIn)
                    {
                        _logger.LogError("GatewayConfig.RequireCompressedFormat is set to true. Backend servers must use compression");
                        _loggedIn = false;
                    }
                }
            }
        }

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
            WritePacket(packet);
        }

        public Task ReceiveTilClosedAndDispose()
        {
            return Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var packet = ReadPacket();
                        _receiver.Forward(packet);
                    }
                }
                catch (MCConnectionClosedException) { throw; }
                catch (Exception ex)
                {
                    if (ex is IOException)
                        throw new MCConnectionClosedException();
                    if (GatewayLogging.Config.LogServerInvalidDataException && ex is InvalidDataException)
                        _logger.LogWarning(ex, "InvalidDataException occurred while ServerConnection was receiving");
                    else
                        _logger.LogWarning(ex, "Uncaught exception occurred while ServerConnection was receiving");

                    throw new MCConnectionClosedException();
                }
                finally
                {
                    Dispose();
                }
            });
        }


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
