using JTJabba.EasyConfig;
using MCGateway.Protocol.Versions.P759_G1_19;


namespace PingPongDemo.InterceptionServices.Services
{
    public class ChatService : GrpcServiceBase<Messager.MessagerClient, MessageConfirmation>, IPlayerService
    {
        private readonly ILogger logger;
        private readonly CancellationTokenSource cancellationTokenSource = new();

        public ChatService( ILogger<ChatService> logger)
        {
            this.logger = logger;
            Channel = CreateAndStartChannel();
            logger.LogInformation("Chat Service initialized");
        }

        public override ILogger GetLogger() => logger;
        public override string GetServiceName() => "Chat Service";
        public override string GetServerIp() => Config.Services.ChatService.IP;
        public override int GetServerPort() => Config.Services.ChatService.Port;
        public override CancellationTokenSource GetCancellationTokenSource() => cancellationTokenSource;

        public override void ProcessReply(MessageConfirmation reply)
        {
            logger.LogInformation("Received reply: " + reply.Status);
        }
     
        public Task OnPlayerJoin(Guid playerUuid, string playerName)
        {
            var reply = Client?.AddPlayerAsync(new PlayerJoinRequest { Uuid = playerUuid.ToString(), PlayerName = playerName });

            if (reply == null)
            {
                logger.LogError("Reply received was null");
                return Task.CompletedTask;
            }
            return reply.ResponseAsync;
        }
        public Task OnPlayerLeave(Guid playerUuid, string playerName)
        {
            var reply = Client?.RemovePlayerAsync(new PlayerLeaveRequest { Uuid = playerUuid.ToString(), PlayerName = playerName });

            if (reply == null)
            {
                logger.LogError("Reply received was null");
                return Task.CompletedTask;
            }
            return reply.ResponseAsync;
        }

        public void AcceptChatMessagePacket(Packet packet, Guid senderId)
        {
            string message = packet.ReadString();
            logger.LogInformation(message);

            var reply = Client?.SendMessageAsync(new MessageRequest { Uuid = senderId.ToString(), Message = message });

            if (reply == null)
            {
                logger.LogError("Reply received was null");
                return;
            }

            AddReplyTask(reply.ResponseAsync);
        }
    }
}
