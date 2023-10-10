using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCGateway.Protocol.V759;

namespace TestGateway1
{
    internal class MainReceiver : IServerboundReceiver
    {
        IServerboundReceiver _receiver;
        ChatReceiver _chatReceiver;

        public MainReceiver(IServerboundReceiver receiver)
        {
            _receiver = receiver;
            _chatReceiver = new ChatReceiver();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Forward(Packet packet)
        {
            switch (packet.PacketID)
            {

                case 0x04:
                    _chatReceiver.AcceptChatMessagePacket(packet);
                    break;
                default:
                    _receiver.Forward(packet);
                    break;
            }
        }
    }
}
