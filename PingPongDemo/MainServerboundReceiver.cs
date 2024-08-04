using MCGateway.Protocol.Versions.P759_G1_19;
using Microsoft.Extensions.DependencyInjection;
using PingPongDemo.MCClientConCallbackFactories;
using PingPongDemo.ServerboundReceivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo
{
    internal class MainServerboundReceiver : IServerboundReceiver
    {
        IServerboundReceiver _receiver;
        PingPongReceiver _pingPongReceiver;
        ServerChatReceiver _chatReceiver;
        Guid _id;

        public static void Initialize()
        {
            ServerChatReceiver.Initialize();
        }

        public MainServerboundReceiver(MainFactoryServiceContainer serviceContainer, IServerboundReceiver receiver, Guid Uuid)
        {
            _id = Uuid;
            _receiver = receiver;
            _chatReceiver = new ServerChatReceiver(Uuid);
            _pingPongReceiver = new PingPongReceiver(serviceContainer.PingPongService_,
                Uuid,
                forwardTo: receiver);
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
                    if (!_pingPongReceiver.TryInterceptPing(packet)) _chatReceiver.AcceptChatMessagePacket(packet);
                    break;
                default:
                    _receiver.Forward(packet);
                    break;
            }
        }

        public static void Close()
        {
            ServerChatReceiver.Close();
        }
    }
}
