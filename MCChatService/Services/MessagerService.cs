using Grpc.Core;

namespace MCChatService.Services
{
    public class MessagerService : Messager.MessagerBase
    {

        private readonly ILogger<MessagerService> _logger;
        public MessagerService(ILogger<MessagerService> logger)
        {
            _logger = logger;
        }

        public override Task<MessageConfirmation> SendMessage(MessageRequest request, ServerCallContext context)
        {

            _logger.LogInformation($"Received request from {request.Uuid}: {request.Message}");


            return Task.FromResult(new MessageConfirmation
            {
                Status = "Message Received"
            });
        }

    }
}
