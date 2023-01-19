using JTJabba.EasyConfig;

namespace MCGateway
{
    public static class GatewayConfig
    {
        #region INITIALIZATION_CODE
        static void ValidateConfig()
        {
            if (RequireCompressedFormat)
            {
                if (Config.CompressionThreshold < 0)
                    throw new ArgumentException(
                        "GatewayConfig.RequireCompressedFormat is set to true. Config.CompressionThreshold must be greater than 0");
            }
        }

        static GatewayConfig()
        {
            JTJabba.EasyConfig.Loader.ConfigLoader.AddOnFirstStaticLoadCallback(ValidateConfig);

            if (LittleEndian != BitConverter.IsLittleEndian)
                throw new ArgumentException("MCGateway compiled for wrong endianess");
        }


        #endregion
        #region CONFIG


#if DEBUG
        public const bool InDebug = true;
#else
        public const bool InDebug = false;
#endif
        public const int StackScratchpadSize = 1024;
        public const bool RequireCompressedFormat = true; // Speeds up packet handling
        public const bool LittleEndian = true;

        public static class Debug
        {
            public const bool CheckPacketIDsDuringLogin = InDebug & true;
        }

        #endregion
    }
}
