using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MCService
{
    // Implementation of a gRPC service host for managing clients and service handlers
    public class MCGrpcService : IMCService
    {
        // Internal builder for setting up the application
        private WebApplicationBuilder? Builder;

        // The final built application (used to run and stop the server)
        private WebApplication? App;

        // List to store all registered client connections
        private readonly List<ClientConnection> clientConnections = new List<ClientConnection>();

        // Adds a gRPC client connection for a specified client class
        public void AddClientConnection<TClientClass>(ClientConnection clientConnection) where TClientClass : class
        {
            // Ensure the builder is initialized before adding services
            if (Builder == null) throw new Exception("Please start the builder before you add to it");

            // Store the client connection
            clientConnections.Add(clientConnection);

            // Register the gRPC client with the host address and certificate validation
            Builder.Services.AddGrpcClient<TClientClass>(o =>
            {
                o.Address = new Uri(clientConnection.hostAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();

                // Custom certificate validation to trust only the specified certificate
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    return cert.Equals(clientConnection.certificate);  // Trust this specific certificate
                };

                return handler;
            });
        }

        // Registers a singleton service handler with the DI container
        public void AddHandler<TServiceHandler>() where TServiceHandler : class
        {
            if (Builder == null) throw new Exception("Please start the builder before you add to it");

            Builder.Services.AddSingleton<TServiceHandler>();
        }

        // Initializes the builder and configures logging and gRPC services
        public void Start(string[] args)
        {
            // Create a new application builder with command-line arguments
            Builder = WebApplication.CreateBuilder(args);

            // Register gRPC framework with dependency injection
            Builder.Services.AddGrpc();

            // Configure logging to output to console
            Builder.Logging.AddConsole();
            Builder.Logging.SetMinimumLevel(LogLevel.Information); // You can change this level for more/less verbosity
        }

        // Builds and runs the app with specified gRPC service handler
        public void Build<TService>() where TService : class
        {
            if (Builder == null) throw new Exception("Please start the builder before you add to it");

            // Build the application pipeline
            App = Builder.Build();

            // Apply security-related HTTP headers
            App.UseHsts();

            // Enable routing for endpoint mapping
            App.UseRouting();

#pragma warning disable ASP0014 // Suppresses warning for using old-style endpoint registration
            App.UseEndpoints(endpoints =>
            {
                // Maps the specified gRPC service to an endpoint
                endpoints.MapGrpcService<TService>();

                // Adds a basic fallback HTTP GET route
                endpoints.MapGet("/", () =>
                    "Communication with gRPC endpoints must be made through a gRPC client. " +
                    "To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            });
#pragma warning restore ASP0014

            // Start running the application
            App.Run();
        }

        // Disposes of the application and cleans up resources
        public async void Stop()
        {
            if (App == null) throw new Exception("Please build the app before you close it");

            await App.DisposeAsync();
        }
    }
}
