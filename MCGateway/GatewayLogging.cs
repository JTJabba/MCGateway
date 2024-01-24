using Microsoft.Extensions.Logging.Abstractions;
using MCGateway.Protocol;

namespace MCGateway
{
    public static class GatewayLogging
    {
        public static ILoggerFactory LoggerFactory { get; internal set; } = new NullLoggerFactory();
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
        public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);


        public static void LogPacket(
            ILogger logger,
            LogLevel level,
            bool trueIfReadingElseWriting,
            IMCConnection con,
            string message,
            int? packetID = null,
            Exception? ex = null)

        {
            logger.Log(
                level,
                ex,
                "Packet debug: connectionType={connectionType}, ititTimestamp={initTimestamp} accountUUID={accountUUID}, packetNumber=0x{packetNumber}, packetID=0x{packetID}, direction={readOrWrite}: {message}",
                con is IMCClientConnection ? "server" : "client",
                con.InitTimestamp,
                con.UUID,
                con.PacketsRead.ToString("X"),
                packetID?.ToString("X"),
                trueIfReadingElseWriting ? "Reading" : "Writing",
                message
                );
        }
    }
}
