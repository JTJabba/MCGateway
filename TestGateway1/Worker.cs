using System.Runtime.Versioning;
using MCGateway;
using JTJabba.EasyConfig;

namespace TestGateway1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _loggerFactory.AddFile($"{Directory.GetCurrentDirectory()}\\Logs\\log.txt");
        }

        [RequiresPreviewFeatures]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var gateway = new Gateway<GatewayConCallback>(new string[]{"settings.MCGateway.json", "translations.MCGateway.json"}, stoppingToken, _loggerFactory);
            gateway.StartListening();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}