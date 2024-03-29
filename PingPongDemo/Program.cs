using JTJabba.EasyConfig.Loader;
using MCGateway;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.MCClientConCallbackFactories;

namespace PingPongDemo
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(ConfigureServices)
                .Build();

            await host.RunAsync();
        }

        static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddFile($"{Directory.GetCurrentDirectory()}\\Logs\\log.txt");
            });

            LoadConfig();
            GatewayConfig.StartupChecks();
            services.AddSingleton<ConnectionsDictionary>();
            services.AddSingleton<IMCClientConCallbackFactory, MC4FactoryMain>();
            services.AddSingleton<IGatewayConnectionCallback, GatewayConCallback>();
            services.AddSingleton<IGateway, Gateway>();
            services.AddHostedService<Worker>();
        }

        static void LoadConfig()
        {
            string[] configPaths = { "settings.MCGateway.json", "translations.MCGateway.json" };
            ConfigLoader.Load(configPaths);
        }
    }
}