using MCChatService;
using MCChatService.Services;
using MCChatService.Services.ChatResponse;
using MCService;

public class Program
{
    public static void Main(string[] args)
    {
        var serviceContainer = new MCGrpcService();

        serviceContainer.Start(args);

        var hostAddress = "https://192.168.4.28:25576";
        var certificateLocation = "C:\\Users\\redbo\\source\\repos\\Kingmo\\MCChatService\\crypto\\server.crt";

        serviceContainer.AddClientConnection<ClientChatMessager.ClientChatMessagerClient>(new ClientConnection(hostAddress, certificateLocation));

        serviceContainer.AddHandler<ClientChatMessagerHandler>();

        serviceContainer.Build<MessagerService>();
    }
}



