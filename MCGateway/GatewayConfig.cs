namespace MCGateway
{
    public static class GatewayConfig
    {
#if DEBUG
        public const bool InDebug = true;
#else
        public const bool InDebug = false;
#endif
        public const int StackScratchpadSize = 1024;
        public const bool RequireCompressedFormat = true; // Speeds up packet handling

        public static class Debug
        {
            public const bool CheckPacketIDsDuringLogin = InDebug & true;
        }
    }
}
