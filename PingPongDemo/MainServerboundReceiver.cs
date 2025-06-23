using MCGateway.Protocol.Versions.P759_G1_19;
using Microsoft.Extensions.DependencyInjection;
using PingPongDemo.InterceptionServices;
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
        private readonly IServerboundReceiver _receiver;
        private readonly PingPongReceiver _pingPongReceiver;
        private readonly ServerChatReceiver _chatReceiver;
        private readonly ServiceManager _serviceManager;
        private readonly Guid _id;

        public MainServerboundReceiver(MainFactoryServiceContainer serviceContainer, ServiceManager serviceManager, IServerboundReceiver receiver, Guid Uuid)
        {
            _id = Uuid;
            _receiver = receiver;
            _chatReceiver = new ServerChatReceiver(Uuid, serviceManager);
            _pingPongReceiver = new PingPongReceiver(serviceContainer.PingPongService_,
                Uuid,
                forwardTo: receiver);
            _serviceManager = serviceManager;
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
                    if (!_pingPongReceiver.TryInterceptPing(packet)) _chatReceiver.AcceptChatMessage(packet);
                    break;
                default:
                    _receiver.Forward(packet);
                    break;
            }
        }

    }
}
