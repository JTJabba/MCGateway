using MCChatService;
using MCChatService.Services;
using Grpc.Core;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

// Update logging settings if needed
builder.Logging.AddConsole(); // Ensures logs are written to the console
builder.Logging.SetMinimumLevel(LogLevel.Information); // Adjust log level as needed

var app = builder.Build();

// Configure the HTTP request pipeline with SSL
app.UseHsts();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    // Map gRPC service
    endpoints.MapGrpcService<MessagerService>();

    // Additional routes can be added if needed
    endpoints.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
});

app.Run();