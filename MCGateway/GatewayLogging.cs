using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using MCGateway.Protocol;

namespace MCGateway
{
    public static class GatewayLogging
    {
#if DEBUG
        public const bool InDebug = true;
#else
        public const bool InDebug = false;
#endif
        public static ILoggerFactory LoggerFactory { get; internal set; } = new NullLoggerFactory();
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
        public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);

        /// <summary>
        /// If in debug, asserts that packet IDs match or throws error.
        /// If successLogMessage and logger are not null, will log message with packet id as argument
        /// </summary>
        [Conditional("DEBUG")]
        public static void DebugAssertPacketID(int expected, int actual, ILogger? logger = null, string? successLogMessage = null)
        {
            if (expected != actual) throw new InvalidDataException($"Expected Packet ID {expected.ToString("X")}, got {actual.ToString("X")} instead");
            if (successLogMessage != null && logger != null)
                logger.LogDebug(successLogMessage, expected);
        }

        [Conditional("DEBUG")]
        public static void DebugPacket(
            IMCConnection con,
            ILogger logger,
            ulong packetNumber,
            string message)

        {
            logger.LogDebug(
                "Packet debug: connectionType={connectionType}, accountUUID={accountUUID}, packetNumber=0x{packetNumber}: {message}",
                con is IMCClientConnection ? "server" : "client",
                con.UUID,
                packetNumber.ToString("X"),
                message
                );
        }

        public static class Config
        {
            public const bool LogDisconnectsPlayState = InDebug & true;
        }
    }
}
