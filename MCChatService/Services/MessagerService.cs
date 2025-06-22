using Grpc.Core;
using MCChatService.Services.ChatResponse;

namespace MCChatService.Services
{
    public class MessagerService : Messager.MessagerBase
    {

        private readonly ILogger<MessagerService> _logger;
        private readonly ClientChatMessagerHandler _messageHandler;

        public MessagerService(ILogger<MessagerService> logger, ClientChatMessagerHandler messageHandler)
        {
            _logger = logger;
            _messageHandler = messageHandler;
        }

        public override Task<MessageConfirmation> SendMessage(MessageRequest request, ServerCallContext context)
        {

            _logger.LogInformation($"Received request from {request.Uuid}: {request.Message}");

            List<string> uuidList = new List<string> { request.Uuid.ToString() };

            Task task = _messageHandler.SendFinishedChatBack(uuidList, request.Message);

            return Task.FromResult(new MessageConfirmation
            {
                Status = "Message Received"
            });
        }

    }
}
