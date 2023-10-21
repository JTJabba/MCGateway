using MCGateway;
using TestGateway1;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLogging(builder =>
        {
            builder.AddFile($"{Directory.GetCurrentDirectory()}\\Logs\\log.txt");
        });

        // Register Gateway as a singleton
        services.AddSingleton(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new Gateway<GatewayConCallback>(new string[] { "settings.MCGateway.json", "translations.MCGateway.json" }, loggerFactory);
        });

        services.AddHostedService<Worker>();

    })
    .Build();

await host.RunAsync();
