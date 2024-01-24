using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;

namespace MCGateway
{
    /// <summary>
    /// Contains switches for conditional compilation and logic for validating the config and environment.
    /// </summary>
    public static class GatewayConfig
    {
        static bool _configValidated = false;

#if DEBUG
        public const bool InDebug = true;
#else
        public const bool InDebug = false;
#endif
        /// <summary>
        /// Speeds up packet handling.
        /// </summary>
        public const bool RequireCompressedFormat = true; // Should be checked in ValidateConfig
        // public const bool LittleEndian = true; // Should be checked in StartupChecks. Be careful using; Gateway will eventually contain utilities used independently of the normal entry point

        public static class Debug
        {
            public const bool LogHandshakeAndStatusFlow = InDebug & false;
            public const bool CheckPacketIDsDuringLogin = InDebug & true;
            public const bool LogLoginConnectionFlow = InDebug & true;
            public const bool LogClientInvalidDataException = InDebug & true;
            public const bool LogServerInvalidDataException = true;
        }

        static void ValidateConfig()
        {
            if (RequireCompressedFormat)
            {
                if (Config.CompressionThreshold < 0)
                    throw new ArgumentException(
                        "GatewayConfig.RequireCompressedFormat is set to true. Config.CompressionThreshold must be greater than 0");
            }

            _configValidated = true;
        }

        static bool _startupCompleted = false;
        static object _startupLock = new object();
        public static void StartupChecks()
        {
            lock (_startupLock)
            {
                if (_startupCompleted) return;

                ConfigLoader.AddOnFirstStaticLoadCallback(ValidateConfig);

                if (!_configValidated)
                    throw new ApplicationException("EasyConfig should be loaded at start of program!");

                //if (LittleEndian != BitConverter.IsLittleEndian)
                //    throw new ArgumentException("MCGateway compiled for wrong endianess");

                _startupCompleted = true;
            }
        }
    }
}
