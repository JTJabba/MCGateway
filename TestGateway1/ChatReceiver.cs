using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCGateway;
using MCGateway.Protocol.V759;

namespace TestGateway1
{
    public class ChatReceiver 
    {

        ILogger _logger = GatewayLogging.CreateLogger<ChatReceiver>();

        public void Dispose()
        {
        }

        public void AcceptChatMessagePacket(Packet packet)
        {
            string message = packet.ReadString();
            _logger.LogInformation(message);
        }

    }
}
