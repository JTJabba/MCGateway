using System.Net.Sockets;
using JTJabba.EasyConfig;

namespace MCGateway.Protocol
{
    public interface IMCConnection : IDisposable
    {
        public TcpClient Client { get; }
        public int ProtocolVersion { get; }
        public string Username { get; }
        public Guid UUID { get; }
        public Config.TranslationsObject ClientTranslation { get; set; }
    }
}
