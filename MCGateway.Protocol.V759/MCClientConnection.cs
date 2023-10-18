using MCGateway.Protocol.V759.DataTypes;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JTJabba.EasyConfig;
using MCGateway.Utils.Types;
using MCGateway.Protocol.Crypto;
using static MCGateway.Protocol.IMCClientConnection;

namespace MCGateway.Protocol.V759
{
    [SkipLocalsInit]
    public sealed class MCClientConnection<ConnectionCallback> : MCConnection, IMCClientConnection, IClientboundReceiver
        where ConnectionCallback : IMCClientConnectionCallback
    {
        
        readonly ILogger _logger = GatewayLogging.CreateLogger<MCClientConnection<ConnectionCallback>>();
        readonly bool _loggedIn = false;
        readonly IMCClientConnectionCallback _callback;

        static readonly RSACryptoServiceProvider RSAProvider = new(1024);
        static readonly byte[] RSAPublicKey;

        /// <summary>
        /// Contains all bytes for encryption request except verify token
        /// </summary>
        static readonly byte[] EncryptionRequestHeader;
        static readonly byte[] SetCompressionPacket;

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
        MCClientConnection(
            TcpClient tcpClient,
            TryAddOnlinePlayer tryAddOnlinePlayerCallback,
            RemoveOnlinePlayer removeOnlinePlayerCallback)
            : base(tcpClient, Config.BufferSizes.ServerBound, (ulong)DateTime.UtcNow.Ticks)
#pragma warning restore CS8618
        {
            // Need to do login process and get client to play state
            try
            {
                // Read login request
                {
                    using var loginRequest = ReadPacketLogin();

                    if (GatewayConfig.Debug.CheckPacketIDsDuringLogin)
                    {
                        if (loginRequest.PacketID != 0x00)
                        {
                            GatewayLogging.LogPacket(
                                _logger, LogLevel.Debug, true, this,
                                "Invalid packet ID. Expecting 0x00",
                                loginRequest.PacketID);
                            throw new InvalidDataException();
                        }
                    }

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

                string? skin = null;
                if (Config.OnlineMode)
                {
                    // Send encryption request
                    Span<byte> verifyTokenBytes = stackalloc byte[4];
                    Random.Shared.NextBytes(verifyTokenBytes);
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
                        using var encryptionResponse = ReadPacketLogin();

                        if (GatewayConfig.Debug.CheckPacketIDsDuringLogin)
                        {
                            if (encryptionResponse.PacketID != 0x01)
                            {
                                GatewayLogging.LogPacket(
                                    _logger, LogLevel.Debug, true, this,
                                    "Invalid packet ID. Expecting 0x01",
                                    encryptionResponse.PacketID);
                                throw new InvalidDataException();
                            }
                        }

                        encryptionResponse.MoveCursor(2); // Skip key length, always 128
                        sharedKey = RSAProvider.Decrypt(encryptionResponse.ReadBytes(128).ToArray(), false);
                        if (encryptionResponse.ReadBool()) // Has verify token
                        {
                            encryptionResponse.MoveCursor(1);
                            if (!encryptionResponse.ReadBytes(4).SequenceEqual(verifyTokenBytes)) return;
                        }
                    }

                    // Wrap netStream in AesCfb8Stream to handle encryption
                    if (GatewayLogging.InDebug) _logger.LogDebug("Enabling encryption");
                    _stream =
                        FastAesCfb8Stream.IsSupported
                        ? new FastAesCfb8Stream(_stream, sharedKey)
                        : new AesCfb8Stream(_stream, sharedKey);

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
                        using var authCancelSource = new CancellationTokenSource();
                        using var httpClient = new HttpClient();
                        using var httpRequest = httpClient.GetAsync(requestUrl.ToString(), authCancelSource.Token);

                        // Wait for response or timeout
                        bool authCompleted = httpRequest.Wait(10_000);

                        if (!authCompleted)
                        {
                            _logger.LogDebug("timed out?");
                            authCancelSource.Cancel();
                            LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationTimeout);
                            return;
                        }

                        HttpResponseMessage httpResponse;
                        try
                        {
                            httpResponse = httpRequest.Result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error authenticating with Mojang endpoint");
                            return;
                        }
                        try
                        {
                            string? responseString = httpResponse.Content.ReadAsStringAsync().Result;
                            if (responseString is null || responseString.Length == 0)
                            {
                                if (GatewayLogging.InDebug) _logger.LogDebug($"Got {(responseString is null ? "null" : "zero")} length response");
                                LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationFail);
                                return;
                            }
                            authResponse = JsonSerializer.Deserialize<AuthResponse>(httpResponse.Content.ReadAsStringAsync().Result);
                        }
                        finally
                        {
                            httpResponse.Dispose();
                        }
                    }

                    // Check auth response
                    if (authResponse?.id == null)
                    {
                        if (GatewayLogging.InDebug) _logger.LogDebug("Authentication fail");
                        LoginDisconnect(Translation.DefaultTranslation.DisconnectAuthenticationFail);
                        return;
                    }
                    _logger.LogDebug("Authenticated!");
                    skin = authResponse.properties[0].value;
                    UUID = Guid.Parse(authResponse.id);
                }

                if (!Config.OnlineMode) UUID = Keccak.HashStringToGuid(Username);

                // Add online player or return if already online
                if (!tryAddOnlinePlayerCallback.Invoke(Username, UUID))
                {
                    LoginDisconnect(Translation.DefaultTranslation.DisconnectPlayerAlreadyOnline);
                    return;
                }

                // Finish login or remove from online players
                try
                {
                    using var callbackCancelSource = new CancellationTokenSource();
                    using var getCallback = ConnectionCallback.GetCallback(
                        Username, UUID, skin, this, callbackCancelSource.Token);


                    // Compression packet
                    _logger.LogDebug("writing compression packet");
                    if (GatewayConfig.RequireCompressedFormat || Config.CompressionThreshold >= 0)
                    {
                        _stream.Write(SetCompressionPacket);
                        _compressionThreshold = Config.CompressionThreshold;
                    }
                    _logger.LogDebug("sent compression packet");
                    // Send login success
                    {
                        int offset = Packet.SCRATCHSPACE;
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(128);
                        try
                        {
                            buffer[offset++] = 0x02; // Packet id
                            UUID.TryWriteBytes(buffer.AsSpan(offset)); // UUID
                            offset += 16;
                            offset += Packet.WriteString(buffer.AsSpan(offset), Username); // Username
                            buffer[offset++] = 0x00; // Number of properties
                        }
                        catch
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                            throw;
                        }

                        WritePacket(new Packet(buffer, offset, 0, 0x02, 1));
                    }
                    _logger.LogDebug("sent login success");
                    try
                    {
                        bool getCallbackComplete = getCallback.Wait(8000);
                        if (!getCallbackComplete)
                        {
                            _logger.LogWarning("getCallback timed out");
                            callbackCancelSource.Cancel();
                            Disconnect(Translation.DefaultTranslation.DisconnectBackendTimeout);
                            return;
                        }
                    }
                    catch (AggregateException ex)
                    {
                        throw new Exception("Exception occured while getting callback", ex);
                    }
                    
                    _callback = (IMCClientConnectionCallback)getCallback.Result;
                    _logger.LogDebug("Got callback");
                    ClientTranslation = _callback.GetTranslationsObject();

                    _loggedIn = true;
                    return;
                }
                catch
                {
                    removeOnlinePlayerCallback.Invoke(UUID);
                    throw;
                }
            }
#if DEBUG
            catch (Exception ex) { _logger.LogDebug(ex, $"Exception occurred during login process"); }
#else
            catch { }
#endif

            // Needs to be updated if used after a set compression packet is sent
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
        public static MCClientConnection<MCConnectionCallback>? GetLoggedInClientConnection
            <MCConnectionCallback>(
            TcpClient tcpClient,
            TryAddOnlinePlayer tryAddOnlinePlayerCallback,
            RemoveOnlinePlayer removeOnlinePlayerCallback)
            where MCConnectionCallback : IMCClientConnectionCallback
        {
            var con = new MCClientConnection<MCConnectionCallback>
                (tcpClient, tryAddOnlinePlayerCallback, removeOnlinePlayerCallback);
            return con._loggedIn ? con : null;
        }

        public void Disconnect(string reason) // This should use an auto generated library for making packet in future
        {
            _logger.LogWarning("Disconnect called but not implemented. Reason: " + reason);

            //int reasonLength = Encoding.UTF8.GetByteCount(reason);
            //int reasonLengthLength = Packet.GetVarIntLength(reasonLength);
            //int packetIdLength = Packet.GetVarIntLength((int)ClientboundPacketType.DISCONNECT);
            //int packetLength = reasonLength + reasonLengthLength + packetIdLength;
            //var bytes = ArrayPool<byte>.Shared.Rent(Packet.GetVarIntLength(packetLength) + packetLength);
            //int offset = Packet.WriteVarInt(bytes, 0, packetLength);
            //offset += Packet.WriteVarInt(bytes, offset, (int)ClientboundPacketType.DISCONNECT);
            //offset += Packet.WriteVarInt(bytes, offset, reasonLength);
            //Packet.WriteString(bytes, reason);
            // Need to finish
        }

        public void Forward(Packet packet)
        {
            WritePacket(packet);
        }

        public Task ReceiveTilClosedAndDispose()
        {
            _callback.StartedReceivingCallback();
            return Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        using var packet = ReadPacket();
                        _callback.Forward(packet);
                    }
                }
                catch (MCConnectionClosedException) { throw; }
                catch (Exception ex)
                {
                    if (ex is IOException)
                        throw new MCConnectionClosedException();
                    if (GatewayLogging.Config.LogClientInvalidDataException && ex is InvalidDataException)
                        _logger.LogDebug(ex, "InvalidDataException occurred while ClientConnection was receiving");
                    else
                        _logger.LogWarning(ex, "Uncaught exception occurred while ClientConnection was receiving");

                    throw new MCConnectionClosedException();
                }
                finally
                {
                    Dispose();
                }
            });
        }

        class AuthResponse
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


        protected override void Dispose(bool disposing)
        {
            if (isDisposed) return;
            base.Dispose(disposing); // Dispose first so this can't be reentered
            if (disposing)
            {
                PlayerPubKey?.Dispose();
                _callback.Dispose();
            }
        }
    }
}
