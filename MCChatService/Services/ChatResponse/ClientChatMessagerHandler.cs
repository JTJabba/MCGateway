namespace MCChatService.Services.ChatResponse
{
    public class ClientChatMessagerHandler
    {
        private readonly ClientChatMessager.ClientChatMessagerClient _client;
        private readonly ILogger<ClientChatMessagerHandler> _logger;


        public ClientChatMessagerHandler(ClientChatMessager.ClientChatMessagerClient client , ILogger<ClientChatMessagerHandler> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task SendFinishedChatBack(List<string> uuids, string message)
        {
            var request = new ClientChatMessageRequest{ Uuids = { uuids } , Message = message };

            try
            {
                var response = await _client.ReceiveMessageAsync(request);
                _logger.LogInformation($"Message sent successfully. Response: {response}", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message.");
                throw;
            }
        }
    }
}
