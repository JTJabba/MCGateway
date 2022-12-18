using System.Net.Sockets;
using JTJabba.EasyConfig;
using JTJabba.Utils;

namespace MCGateway.Protocol
{
    public interface IMCClientConnection : IMCConnection
    {
        public Task ReceiveTilClosedAndDispose();
        public void Disconnect(string reason);
    }
}
