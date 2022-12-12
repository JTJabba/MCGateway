using MCGateway;
using MCGateway.Protocol.V759.DataTypes;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JTJabba.EasyConfig;
using JTJabba.Utils;
using MCGateway.Protocol.Crypto;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public sealed class MCClientConnection<ConnectionCallback> : MCConnection, IMCClientConnection, IClientBoundReceiver
        where ConnectionCallback : IMCClientConnectionCallback
    {
        private readonly ILogger _logger = GatewayLogging.CreateLogger<MCClientConnection<ConnectionCallback>>();
        private readonly bool _loggedIn = false;
        [RequiresPreviewFeatures]
        private readonly IMCClientConnectionCallback _callback;

        private static readonly RSACryptoServiceProvider RSAProvider = new(1024);
        private static readonly byte[] RSAPublicKey;

        /// <summary>
        /// Contains all bytes for encryption request except verify token
        /// </summary>
        private static readonly byte[] EncryptionRequestHeader;

        private static readonly byte[] SetCompressionPacket;

        public override string Username { get; init; }
        public override Guid UUID { get; init; }
        public override Config.TranslationsObject ClientTranslation { get; set; }
        public PlayerPublicKey? PlayerPubKey { get; init; }

        static MCClientConnection()
        {
            RSAPublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                DotNetUtilities.GetRsaPublicKey(RSAProvider.ExportParameters(false))).GetDerEncoded();

            // Need 169 bytes - 2 bytes for length, 1 packet id, 1 empty server id string, 2 public key length, 162 public key, 1 verify token length
            EncryptionRequestHeader = new byte[169];
            EncryptionRequestHeader[0] = 0xAB; // Packet length
            EncryptionRequestHeader[1] = 0x01;
            EncryptionRequestHeader[2] = 0x01; // Packet ID
            EncryptionRequestHeader[3] = 0x00; // Server string length
            EncryptionRequestHeader[4] = 0xA2; // Public key length
            EncryptionRequestHeader[5] = 0x01;
            RSAPublicKey.CopyTo(EncryptionRequestHeader.AsSpan(6)); // Public key
            EncryptionRequestHeader[168] = 0x04; // Verify token length

            if (Config.CompressionThreshold > 0)
            {
                int packetLength = Packet.GetVarIntLength(Config.CompressionThreshold)
                    + 1; // Packet ID
                int packetLengthLength = Packet.GetVarIntLength(packetLength);
                SetCompressionPacket = new byte[packetLengthLength + packetLength];
                Packet.WriteVarInt(SetCompressionPacket, 0, packetLength);
                SetCompressionPacket[packetLengthLength] = 0x03;
                Packet.WriteVarInt(SetCompressionPacket, packetLengthLength + 1, Config.CompressionThreshold);
            }
            else
            {
                SetCompressionPacket = Array.Empty<byte>();
            }
        }

#pragma warning disable CS8618 // Object wont be returned from public method if it isn't valid
        [RequiresPreviewFeatures]
        private MCClientConnection(TcpClient tcpClient) : base(tcpClient, Config.BufferSizes.ServerBound)
#pragma warning restore CS8618
        {
            // Need to do login process and get client to play state
            try
            {
                // Read login request
                {
                    int packetLength = ReadVarInt();
                    using var loginRequest = new Packet(
                        packetLength,
                        ReadBytes(packetLength));
                    if (GatewayLogging.InDebug) _logger.LogDebug("Recieved login request");
                    int PacketID = loginRequest.ReadByte();
                    GatewayLogging.DebugAssertPacketID(0x00, PacketID);

                    Username = loginRequest.ReadString(16);

                    // If has sig data
                    if (loginRequest.ReadBool())
                    {
                        PlayerPubKey = new PlayerPublicKey(
                            loginRequest.ReadLong(),
                            loginRequest.ReadBytes(loginRequest.ReadVarInt()),
                            loginRequest.ReadBytes(loginRequest.ReadVarInt()));
                    }
                    else if (Config.EnforceSecureProfile)
                    {
                        LoginDisconnect(Translation.DefaultTranslation.DisconnectSecureProfileRequired);
                        return;
                    }
                    // Ignore rest of packet (optional client provided UUID)
                }

                // Send encryption request
                Span<byte> verifyTokenBytes = stackalloc byte[4];
                ThreadStatics.Random.NextBytes(verifyTokenBytes);
                {
                    Span<byte> bytes = stackalloc byte[173];
                    EncryptionRequestHeader.CopyTo(bytes);
                    verifyTokenBytes.CopyTo(bytes[169..]);
                    _stream.Write(bytes);
                    if (GatewayLogging.InDebug) _logger.LogDebug("Sent encryption request");
                }

                // Read encryption response
                byte[] sharedKey;
                {
                    {
                        int packetLength = ReadVarInt();
                        using var encryptionResponse = new Packet(
                            packetLength,
                            ReadBytes(packetLength));
                        if (GatewayLogging.InDebug) _logger.LogDebug("Received encryption response with packet length {len}", encryptionResponse.PacketLength);

                        int PacketID = encryptionResponse.ReadByte();
                        if (GatewayLogging.InDebug) _logger.LogDebug("Received packet ID {id}", PacketID);
                        GatewayLogging.DebugAssertPacketID(0x01, PacketID);

                        encryptionResponse.MoveCursor(2); // Skip key length, always 128
                        sharedKey = RSAProvider.Decrypt(encryptionResponse.ReadBytes(128).ToArray(), false);
                        if (encryptionResponse.ReadBool()) // Has verify token
                        {
                            encryptionResponse.MoveCursor(1);
                            if (!encryptionResponse.ReadBytes(4).SequenceEqual(verifyTokenBytes)) return;
                        }
                    }
                }

                // Wrap netStream in AesCfb8Stream to handle encryption
                if (GatewayLogging.InDebug) _logger.LogDebug("Enabling encryption");
                _stream = FastAesCfb8Stream.IsSupported ? new FastAesCfb8Stream(_stream, sharedKey) : new AesCfb8Stream(_stream, sharedKey);

                // Get auth response from Mojang
                AuthResponse? authResponse;
                {
                    // Get hash
                    if (GatewayLogging.InDebug) _logger.LogDebug("Building auth hash");
                    using var sha1 = SHA1.Create();
                    sha1.TransformBlock(sharedKey, 0, sharedKey.Length, null, 0);
                    sha1.TransformBlock(RSAPublicKey, 0, RSAPublicKey.Length, null, 0);
                    // Use separate final transform with empty array to avoid unnecessary allocation and copy to the array it returns
                    sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    byte[] hash = sha1.Hash!;

                    bool negative = (hash[0] & 0x80) == 0x80;
                    if (negative) // little endian twos complement
                    {
                        int i;
                        bool carry = true;
                        for (i = hash.Length - 1; i >= 0; --i)
                        {
                            hash[i] = (byte)~hash[i];
                            if (carry)
                            {
                                carry = hash[i] == 0xFF;
                                ++hash[i];
                            }
                        }
                    }

                    using var preServerHash = new UnsafeString(Convert.ToHexString(hash));
                    preServerHash.TrimStart('0');
                    using var serverHash = negative ? "-" + preServerHash : preServerHash;

                    if (GatewayLogging.InDebug) _logger.LogDebug("Building auth request");
                    using UnsafeString requestUrl = new("https://sessionserver.mojang.com/session/minecraft/hasJoined?username=", 127);
                    requestUrl.Append(Username);
                    requestUrl.Append("&serverId=");
                    requestUrl.Append(serverHash);

                    if (GatewayLogging.InDebug) _logger.LogDebug("Authenticating with Mojang");

                    // Make auth request
                    using var httpClient = new HttpClient();
                    using var httpRequest = httpClient.GetAsync(requestUrl.ToString());

                    // Wait for response or timeout
                    httpRequest.Wait(10_000);
                    if (!httpRequest.IsCompleted) LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationTimeout);

                    using var httpResponse = httpRequest.Result;

                    string? responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    if (responseString is null || responseString.Length == 0)
                    {
                        if (GatewayLogging.InDebug) _logger.LogDebug("Got null or zero length response");
                        LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationFail);
                        return;
                    }
                    authResponse = JsonSerializer.Deserialize<AuthResponse>(httpResponse.Content.ReadAsStringAsync().Result);
                }

                // Check auth response
                if (authResponse?.id == null)
                {
                    if (GatewayLogging.InDebug) _logger.LogDebug("Authentication fail");
                    LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationFail);
                    return;
                }
                UUID = Guid.Parse(authResponse.id);
                ClientTranslation = ConnectionCallback.GetTranslationsObject(UUID);
                _callback = ConnectionCallback.GetCallback(Username, UUID, ClientTranslation, this);
                if (GatewayLogging.InDebug) _logger.LogInformation("Got callback");
                _callback.SetSkin(authResponse.properties[0].value);

                // Compression packet
                if (GatewayConfig.RequireCompressedFormat || Config.CompressionThreshold >= 0)
                {
                    _stream.Write(SetCompressionPacket);
                    _compressionThreshold = Config.CompressionThreshold;
                }

                // Send login success
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(127);
                    try
                    {
                        int offset = 0;
                        buffer[offset++] = 0x02; // Packet id
                        UUID.TryWriteBytes(buffer.AsSpan(offset)); // UUID
                        offset += 16;
                        offset += Packet.WriteString(buffer.AsSpan(offset), Username); // Username
                        buffer[offset++] = 0x00; // Number of properties
                        _logger.LogInformation("Sending login packet " + Convert.ToHexString(buffer.AsSpan(0, offset)));
                        WritePacket(buffer.AsSpan(0, offset));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                
                _loggedIn = true;
                return;
            }
#if DEBUG
            catch (Exception e) { _logger.LogDebug(e, $"Exception occurred during login process"); }
#else
            catch { }
#endif
            try
            {
                LoginDisconnect("");
            }
#if DEBUG
            catch (Exception e) { _logger.LogDebug(e, $"Exception occurred sending kick packet"); }
#else
            catch { }
#endif

            void LoginDisconnect(string reason)
            {
                int messageLength = Encoding.UTF8.GetByteCount(reason);
                int packetLength =
                    1 +
                    Packet.GetVarIntLength(messageLength) +
                    messageLength;
                int lengthPrefixedPacketLength = Packet.GetVarIntLength(packetLength) + packetLength;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(lengthPrefixedPacketLength);
                try
                {
                    Span<byte> kickPacket = buffer.AsSpan(0, lengthPrefixedPacketLength);

                    // Build packet
                    int offset = Packet.WriteVarInt(kickPacket, 0, packetLength);
                    kickPacket[offset++] = 0x00;
                    offset += Packet.WriteVarInt(kickPacket, offset, messageLength);
                    Encoding.UTF8.GetBytes(reason, kickPacket[offset..]);

                    _stream.Write(kickPacket);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Should take tcpClient that just requested login state
        /// and return null or an IMCClientConnection if it manages to log in the client.
        /// </summary>
        /// <typeparam name="MCConnectionCallback"></typeparam>
        /// <param name="tcpClient"></param>
        /// <returns></returns>
        [RequiresPreviewFeatures]
        public static MCClientConnection<MCConnectionCallback>? GetLoggedInClientConnection
            <MCConnectionCallback>(TcpClient tcpClient)
            where MCConnectionCallback : IMCClientConnectionCallback
        {
            var con = new MCClientConnection<MCConnectionCallback>(tcpClient);
            return con._loggedIn ? con : null;
        }

        public void Disconnect(string reason) // This should use an auto generated library for making packet in future
        {
            _logger.LogWarning("Disconnected called but not implemented");
            if (GatewayLogging.Config.LogDisconnectsPlayState)
                _logger.LogDebug("Player disconnected by server in play state. Reason: '{reason}'", reason);

            int reasonLength = Encoding.UTF8.GetByteCount(reason);
            int reasonLengthLength = Packet.GetVarIntLength(reasonLength);
            int packetIdLength = Packet.GetVarIntLength((byte)ClientboundPacketType.DISCONNECT);
            int packetLength = reasonLength + reasonLengthLength + packetIdLength;
            var bytes = ArrayPool<byte>.Shared.Rent(Packet.GetVarIntLength(packetLength) + packetLength);
            int offset = Packet.WriteVarInt(bytes, 0, packetLength);
            offset += Packet.WriteVarInt(bytes, offset, (int)ClientboundPacketType.DISCONNECT);
            offset += Packet.WriteVarInt(bytes, offset, reasonLength);
            Packet.WriteString(bytes, reason);
            // Need to finish after changing Packet to include packetLength varint in its array
        }

        public void Forward(Packet packet)
        {
            try
            {
                //_logger.LogDebug("Client forwarding packet with ID " + packet.PacketID);
                WritePacket(packet.LengthPrefixedPacketBytes5Offset);
            }
            finally
            {
                packet.Dispose();
            }
        }

        [RequiresPreviewFeatures]
        public Task ReceiveTilClosed()
        {
            _callback.StartedReceivingCallback();
            return Task.Run(() =>
            {
                while (true)
                {
                    var packet = ReadPacket();
                    _logger.LogInformation("Received packet from client with ID {id}", packet.PacketID.ToString("X"));
                    _callback.Forward(packet);
                }
            });
        }

        private class AuthResponse
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles
        {
            public string id { get; set; }
            public string name { get; set; }
            public List<Properties> properties { get; set; }

            public class Properties
            {
                public string name { get; set; }
                public string value { get; set; }
                public string signature { get; set; }
            }
        }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


        [RequiresPreviewFeatures]
        protected override void Dispose(bool disposing)
        {
            if (isDisposed) return;
            base.Dispose(disposing); // Dispose first so this can't be reentered
            if (disposing)
            {
                _callback.Dispose();
            }
        }
    }
}
