using System.Net.Sockets;
using JTJabba.EasyConfig;

namespace MCGateway.Protocol
{
    public interface IMCConnection : IDisposable
    {
        public ulong InitTimestamp { get; }
        public ulong PacketsRead { get; }
        public ulong PacketsWrite { get; }
        public TcpClient Client { get; }
        public int ProtocolVersion { get; }
        public string Username { get; }
        public Guid UUID { get; }
        public Config.TranslationsObject ClientTranslation { get; set; }
    }
}
