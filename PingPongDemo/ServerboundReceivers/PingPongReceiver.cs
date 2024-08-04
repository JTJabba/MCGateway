using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.InterceptionServices;

namespace PingPongDemo.ServerboundReceivers
{
    internal sealed class PingPongReceiver : IServerboundReceiver
    {
        readonly ILogger _logger = GatewayLogging.CreateLogger<PingPongReceiver>();
        IPingPongService _service;
        Guid _clientUuid;
        IServerboundReceiver _receiver;

        public PingPongReceiver(IPingPongService service, Guid clientUuid, IServerboundReceiver forwardTo)
        {
            _service = service;
            _clientUuid = clientUuid;
            _receiver = forwardTo;
        }

        public void Forward(Packet packet)
        {
            switch (packet.PacketID)
            {
                case 0x4:
                    if (!TryInterceptPing(packet)) _receiver.Forward(packet);
                    break;
                default:
                    _receiver.Forward(packet);
                    break;
            }
        }

        public bool TryInterceptPing(Packet packet)
        {
            var msg = packet.ReadString();
            if (msg != "ping") return false;
            _service.PingReceived(_clientUuid);
            _logger.LogDebug("PingPongReceiver intercepted ping");
            return true;
        }

        public void Dispose()
        {
        }
    }
}
