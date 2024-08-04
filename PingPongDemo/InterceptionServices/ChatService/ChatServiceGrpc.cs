using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.ChatService
{
    internal class ChatServiceGrpc : IHostedService
    {
        private readonly ILogger<ChatServiceGrpc> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private Server _server;

        public ChatServiceGrpc(ILogger<ChatServiceGrpc> logger, IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting gRPC server...");

            _server = new Server
            {
                Services = { ClientChatMessager.BindService(new ClientChatMessagerService()) },
                Ports = { new ServerPort("localhost", 25576, ServerCredentials.Insecure) }
            };

            _server.Start();

            _appLifetime.ApplicationStopping.Register(OnStopping);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping gRPC server...");

            _server.ShutdownAsync().Wait();

            return Task.CompletedTask;
        }

        private void OnStopping()
        {
            _server?.ShutdownAsync().Wait();
        }

    }
}
