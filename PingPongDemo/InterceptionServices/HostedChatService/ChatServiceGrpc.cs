using Grpc.Core;
using MCGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.ChatService
{
    internal class ChatServiceGrpc(ILogger<ChatServiceGrpc> logger, IHostApplicationLifetime appLifetime, ConnectionsDictionary connectionsDictionary) : IHostedService
    {
        private readonly ILogger<ChatServiceGrpc> _logger = logger;
        private readonly IHostApplicationLifetime _appLifetime = appLifetime;
        private readonly ConnectionsDictionary _connectionsDictionary = connectionsDictionary;
        private Server? _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting gRPC server...");

            _server = new Server
            {
                Services = { ClientChatMessager.BindService(new ClientChatMessagerService(_connectionsDictionary)) },
                Ports = { new ServerPort("0.0.0.0", 25576, new SslServerCredentials(new List<KeyCertificatePair>
        {
            new KeyCertificatePair(
                File.ReadAllText("/app/crypto/server.crt"),  // Correct path using forward slashes
                File.ReadAllText("/app/crypto/private.key")  // Correct path using forward slashes
            )
        })) }
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
