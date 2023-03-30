

// IGNORE THIS FILE, just scratchspace


using MCGateway;
using MCGateway.Protocol.V759;
using System.Buffers;

namespace TestGateway1
{
    // [ServerboundReceiver(lastHop = true/false)]
    public partial class Receiver
    {
        // [Interceptor]
        void ChatMessage(ChatMessage chatMessage) { }

        // [ConditionalInterceptor]
        bool ClientInformation(ClientInformation clientInformation) { return true; }

        // [Mirror]
        void SetPlayerPosition(SetPlayerPosition setPlayerPosition) { }
    }

    // Generated
    /// <summary>
    /// Targets: V759.
    /// Compatible with: V759, V760, V761.
    /// </summary>
    public sealed partial class Receiver : IServerboundReceiver
    {
        bool _disposed = false;
        IServerboundReceiver _nextHop;

        public Receiver(IServerboundReceiver nextHop) // Generated with no args if lastHop = true
        {
            _nextHop = nextHop;
            Initialize();
        }
        partial void Initialize();

        public void Forward(Packet packet)
        {
            switch ((ServerboundPacketType)packet.PacketID)
            {
                case ServerboundPacketType.CHAT_MESSAGE:
                    ChatMessage(packet.DataField); return;
                case ServerboundPacketType.CLIENT_INFORMATION:
                    if (ClientInformation(packet.DataField)) return; break;
                case ServerboundPacketType.SET_PLAYER_POSITION:
                    SetPlayerPosition(packet.DataField); break;
                default: break;
            }
            _nextHop.Forward(packet); // Not generated if lastHop = true
        }

        // Generate Forward methods for compatible protocols

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true; // Set first to prevent reentry
            _nextHop.Dispose(); // Not generated if lastHop = true
        }
        partial void Dispose(bool disposing);
    }

    ref struct ChatMessage
    {
        ReadOnlySpan<byte> _data;

        public ChatMessage(ReadOnlySpan<byte> dataField)
        {
            _data = dataField;
        }

        public static implicit operator ChatMessage(ReadOnlySpan<byte> span) => new ChatMessage(span);
    }
    ref struct ClientInformation
    {
        ReadOnlySpan<byte> _data;

        public ClientInformation(ReadOnlySpan<byte> dataField)
        {
            _data = dataField;
        }

        public static implicit operator ClientInformation(ReadOnlySpan<byte> span) => new ClientInformation(span);
    }
    ref struct SetPlayerPosition
    {
        ReadOnlySpan<byte> _data;

        public SetPlayerPosition(ReadOnlySpan<byte> dataField)
        {
            _data = dataField;
        }

        public static implicit operator SetPlayerPosition(ReadOnlySpan<byte> span) => new SetPlayerPosition(span);
    }
}