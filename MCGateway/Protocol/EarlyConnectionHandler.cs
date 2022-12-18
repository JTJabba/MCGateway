﻿using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;

namespace MCGateway.Protocol
{
    [SkipLocalsInit]
    public static class EarlyConnectionHandler
    {
        static ILogger logger = GatewayLogging.CreateLogger("EarlyConnectionHandler");
        internal static Dictionary<string, byte[]> CachedClientConnectionStrings = new();

        static readonly byte[] LegacyKickPacket = new byte[]
        {
            0xfe, 0x01, 0xfa, 0x00, 0x0b, 0x00, 0x4d, 0x00,
            0x43, 0x00, 0x7c, 0x00, 0x50, 0x00, 0x69, 0x00,
            0x6e, 0x00, 0x67, 0x00, 0x48, 0x00, 0x6f, 0x00,
            0x73, 0x00, 0x74, 0x00, 0x19, 0x49, 0x00, 0x09,
            0x00, 0x6c, 0x00, 0x6f, 0x00, 0x63, 0x00, 0x61,
            0x00, 0x6c, 0x00, 0x68, 0x00, 0x6f, 0x00, 0x73,
            0x00, 0x74, 0x00, 0x00, 0x63, 0xdd
        };

        static readonly
            (string ServerAddress, ushort ServerPort, int ProtocolVersion)
            DefaultHandshakeReturn = (string.Empty, 0, 0);

        static void LoadCachedConnectionStrings()
        {
            foreach (var conString in Config.CommonClientConnectionStrings)
                CachedClientConnectionStrings.TryAdd(
                    conString,
                    Encoding.UTF8.GetBytes(conString));
        }

        static EarlyConnectionHandler()
        {
            ConfigLoader.AddOnFirstStaticLoadCallback(LoadCachedConnectionStrings);
        }


        /// <summary>
        /// Returns true if loginRequested
        /// </summary>
        [RequiresPreviewFeatures]
        public static unsafe bool TryHandleTilLogin<GatewayConnectionCallback>(
            TcpClient tcpClient,
            out (string ServerAddress, ushort ServerPort, int ProtocolVersion) handshake
            )
            where GatewayConnectionCallback : IGatewayConnectionCallback
        {
            NetworkStream netstream;
            handshake = DefaultHandshakeReturn;

            try
            {
                netstream = tcpClient.GetStream();

                // Handle handshake packet
                int nextState;
                {
                    int packetLength = ReadVarIntRaw();
                    if (packetLength == -1) return false;

                    if (packetLength > 1030) // invalid packet length, might be legacy ping (length would read as 0x3F01)
                    {
                        netstream.Write(LegacyKickPacket);
                        return false;
                    }

                    var handshakePacket = ArrayPool<byte>.Shared.Rent(packetLength);
                    try
                    {
                        RecvBytes(handshakePacket, 0, packetLength);
                        int currentOffset = 1; // Discard packet ID, always 0
                        int protocolVersion = ReadVarInt(handshakePacket, ref currentOffset);
                        string targetServerAddr = ReadTargetServerString(handshakePacket, ref currentOffset);
                        ushort targetServerPort = ReadUShort(handshakePacket, ref currentOffset);
                        nextState = handshakePacket[currentOffset];

                        handshake = (
                            targetServerAddr,
                            targetServerPort,
                            protocolVersion);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(handshakePacket);
                    }
                }
                if (GatewayLogging.InDebug) logger.LogDebug("nextState = " + nextState);
                // Handle status state
                if (nextState == 1)
                {
                    // Read status request
                    if (GatewayLogging.InDebug) logger.LogDebug("Reading status request");
                    {
#pragma warning disable
                        int bytesRemaining = 2;
                        do
                        {
                            int bytesRead = netstream.Read(stackalloc byte[bytesRemaining]);
                            if (bytesRead < 1) return false; // Yikes almost forgot this xd
                            bytesRemaining -= bytesRead;
                        } while (bytesRemaining > 0);
#pragma warning restore
                    }

                    // Send Status Response
                    if (GatewayLogging.InDebug) logger.LogDebug("Sending status response");
                    netstream.Write(GatewayConnectionCallback.GetStatusResponse(handshake));


                    // Ping request and response
                    // Clients may disconnect here.
                    {
                        int packetLength = netstream.ReadByte();

                        // If client didn't understand status response, it will try legacy ping.
                        // If in debug log properly if not assume it understands
                        if (GatewayLogging.InDebug && packetLength == 0xFE) // Should be 0x09 otherwise
                        {
                            logger.LogWarning("Recieved legacy ping after status response. Status response may have been formatted incorrectly");
                            netstream.Write(LegacyKickPacket);
                            return false;
                        }
                        if (packetLength != 0x09) return false;
                        if (GatewayLogging.InDebug) logger.LogDebug("Recieved ping request");
                        var pingRequest = ArrayPool<byte>.Shared.Rent(0x09);
                        try
                        {
                            int bytesRemaining = 0x09;
                            do
                            {
                                int bytesRead = netstream.Read(pingRequest, 0x09 - bytesRemaining, bytesRemaining);
                                if (bytesRead < 1) return false;
                                bytesRemaining -= bytesRead;
                            } while (bytesRemaining > 0);

                            // Allocate array for ping response
                            byte* pingResponsePtr = stackalloc byte[10];
                            var pingReponseSpan = new Span<byte>(pingResponsePtr, 10);

                            // Add packet length and packetID
                            *pingResponsePtr++ = 0x09;
                            *pingResponsePtr++ = 0x01;

                            // Read ping payload into ping response
                            pingRequest.AsSpan(1, 8).CopyTo(pingReponseSpan.Slice(2, 8));

                            // Send ping response
                            netstream.Write(pingReponseSpan);
                            if (GatewayLogging.InDebug) logger.LogDebug("Sent ping response");
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(pingRequest);
                        }
                    }
                }

                // 2 = login
                else if (nextState == 2) return true;
            }
#if DEBUG
            catch (MCConnectionClosedException) { }
            catch (Exception e) { logger.LogDebug(e, "Error while handling early connection"); }
#else
            catch { }
#endif
            return false;


#region "LOCAL_FUNCTIONS"


            int ReadVarIntRaw()
            {
                const int SEGMENT_MASK = 0x7F;
                const int CONTINUE_MASK = 0x80;
                const int MAX_VARINT_LENGTH = 32;
                int value = 0;
                int position = 0;
                int currentByte;

                while (true)
                {
                    currentByte = netstream.ReadByte();
                    if (currentByte == -1) return -1;
                    value |= (currentByte & SEGMENT_MASK) << position;
                    if ((currentByte & CONTINUE_MASK) == 0) return value;
                    position += 7;
                    if (position >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
                }
            }

            int ReadVarInt(byte[] buffer, ref int currentOffset)
            {
                const int SEGMENT_MASK = 0x7F;
                const int CONTINUE_MASK = 0x80;
                const int MAX_VARINT_LENGTH = 32;
                int value = 0;
                int position = 0;
                int currentByte;

                while (true)
                {
                    currentByte = buffer[currentOffset++];
                    value |= (currentByte & SEGMENT_MASK) << position;
                    if ((currentByte & CONTINUE_MASK) == 0) return value;
                    position += 7;
                    if (position >= MAX_VARINT_LENGTH) throw new InvalidDataException("VarInt too big");
                }
            }

            string ReadTargetServerString(byte[] buffer, ref int currentOffset)
            {
                int len = ReadVarInt(buffer, ref currentOffset);
                if (len > 1020) throw new InvalidDataException("TargetServer string oversized");
                string? str = null;
                var bytes = buffer.AsSpan(currentOffset, len);
                currentOffset += len;
                foreach (var cachedStr in CachedClientConnectionStrings)
                    if (bytes.SequenceEqual(cachedStr.Value))
                    {
                        str = cachedStr.Key;
#if DEBUG
                                logger.LogDebug("TargetServerString Cache hit '{string}'", str);
#endif
                    }
                if (str == null)
                {
                    str = Encoding.UTF8.GetString(bytes);
#if DEBUG
                            logger.LogDebug("TargetServerString Cache miss '{string}'", str);
#endif
                    if (str.Length > 255)
                        throw new InvalidDataException("TargetServer string oversized");
                }
                return str;
            }

            ushort ReadUShort(byte[] buffer, ref int currentOffset)
            {
                var value = BinaryPrimitives.ReadUInt16BigEndian(
                    buffer.AsSpan(currentOffset));
                currentOffset += 2;
                return value;
            }

            void RecvBytes(byte[] buffer, int offset, int count)
            {
                do
                {
                    int read = netstream.Read(buffer, offset, count);
                    if (read <= 0) throw new MCConnectionClosedException();
                    offset += read;
                    count -= read;
                } while (count > 0);
            }
#endregion
        }
    }
}