using MCGateway;

namespace PingPongDemo
{
    public class Worker : BackgroundService
    {
        readonly ILogger<Worker> _logger;
        readonly IGateway _gateway;

        public Worker(ILogger<Worker> logger, IGateway gateway)
        {
            _logger = logger;
            _gateway = gateway;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _gateway.StartListening();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(3000, stoppingToken);
            }
            _logger.LogInformation("Cancellation requested. Gateway shutting down...");
            _gateway.Dispose();
        }
    }
}