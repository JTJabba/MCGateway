using MCGateway.Protocol;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;

namespace MCGateway
{
    public interface IGatewayConnectionCallback
    {
        public bool InOfflineMode { get; }

        // Return new instance of a callback. 
        [RequiresPreviewFeatures]
        public static abstract IGatewayConnectionCallback GetCallback((string serverAddress, ushort serverPort, int protocolVersion) handshake);


        // Helper methods 'GetStatusResponseString' and 'GetStatusResponse' are included for building response
        /// <summary>
        /// Returned responses should be used and disgarded immediately.
        /// Implementer is responsible for caching status responses, and keeping spans valid for a period of time.
        /// </summary>
        /// <param name="handshake"></param>
        /// <returns></returns>
        [RequiresPreviewFeatures]
        public static abstract ReadOnlySpan<byte> GetStatusResponse((string ServerAddress, ushort ServerPort, int ProtocolVersion) handshake);
        
        /// <summary>
        /// Should connect to external logic for tracking online players
        /// </summary>
        /// <param name="username"></param>
        /// <param name="uuid"></param>
        /// <returns></returns>
        [RequiresPreviewFeatures]
        public static abstract bool TryAddOnlinePlayer(string username, Guid uuid);

        [RequiresPreviewFeatures]
        public static abstract void RemoveOnlinePlayer(Guid uuid);

        /// <summary>
        /// Will return null if it can't get an authenticated client connection.
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <returns></returns>
        public IMCClientConnection? GetLoggedInClientConnection(TcpClient tcpClient);

        #region HELPER_METHODS


        /// <summary>
        /// Returned byte array should be returned to ArrayPool Shared.
        /// Leave player id blank in <c>playersNameAndIDs</c> for default id.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        protected static string GetStatusResponseString(
            int maxPlayers,
            int onlinePlayers,
            Tuple<string, string?>[] playersNameAndIDs,
            string description,
            string base64Favicon,
            string versionName,
            int protocol,
            bool previewsChat = true
            )
        {
            // Build sample (playerName & id list) field
            var sampleSB = new StringBuilder();
            for (int i = 0; i < playersNameAndIDs.Length; ++i)
            {
                string playerID;
                if (playersNameAndIDs[i].Item2 == null ||
                    playersNameAndIDs[i].Item2 == "")
                    playerID = "00000000-0000-0001-0000-000000000001";
                else playerID = playersNameAndIDs[i].Item2!;

                sampleSB.Append($@"
            {{
                ""name"": ""{playersNameAndIDs[i].Item1}"",
                ""id"": ""{playerID}""
            }}");
                if (playersNameAndIDs.Length > i + 1) sampleSB.Append(',');
            }

            return $@"{{
    ""version"": {{
        ""name"": ""{versionName}"",
        ""protocol"": {protocol}
    }},
    ""players"": {{
        ""max"": {maxPlayers},
        ""online"": {onlinePlayers},
        ""sample"": [{sampleSB}
        ]
    }},
    ""description"": {{
        ""text"": ""{description}""
    }},
    ""favicon"": ""data:image/png;base64,{base64Favicon}"",
    ""previewsChat"": {previewsChat}
}}";
        }

        /// <summary>
        /// Returns length-prefixed status response.
        /// Returned byte array should be returned to ArrayPool Shared.
        /// </summary>
        /// <param name="statusResponse"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        protected static (byte[] buffer, int bytesWritten) GetStatusResponseBytes(string statusResponse)
        {
            // First get length of string
            int statusResponseByteLength = (ushort)Encoding.UTF8.GetByteCount(statusResponse);

            int packetLength =
                statusResponseByteLength
                + GetVarIntLength(statusResponseByteLength) // Length prefix
                + 1; // Packet ID

            // Leave room for length prefix
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength + GetVarIntLength(packetLength));
            try
            {
                // Write packet length
                int bytesWritten = WriteVarInt(buffer, 0, packetLength);

                // Write packet ID
                buffer[bytesWritten++] = 0x00;

                // Write statusResponse length prefix
                bytesWritten += WriteVarInt(buffer, bytesWritten, statusResponseByteLength);

                // Write statusResponse
                Encoding.UTF8.GetBytes(statusResponse, 0, statusResponse.Length, buffer, bytesWritten);
                bytesWritten += statusResponseByteLength;

                return (buffer, bytesWritten);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }

            int GetVarIntLength(int value)
            {
                int length = 1;

                while (!((value & 0xFFFFFF80) == 0))
                {
                    ++length;
                    value >>>= 7;
                }
                return length;
            }

            int WriteVarInt(byte[] buffer, int offset, int value)
            {
                const int SEGMENT_MASK = 0x7F;
                const int CONTINUE_MASK = 0x80;

                int bytesWritten = 0;
                while (true)
                {
                    if ((value & 0xFFFFFF80) == 0)
                    {
                        buffer[offset + bytesWritten++] = (byte)value;
                        return bytesWritten;
                    }
                    buffer[offset + bytesWritten++] = (byte)(value & SEGMENT_MASK | CONTINUE_MASK);
                    value >>>= 7;
                }
            }
        }
        #endregion
    }
}
