using Grpc.Core;
using Grpc.Net.Client;
using PingPongDemo.InterceptionServices.Services;
using System.Net.Security;

public abstract class GrpcServiceBase<TClient, TReply> : IService where TClient : ClientBase
{
    public GrpcChannel? Channel { get; set; }
    public TClient? Client { get; set; }
    private readonly List<Task<TReply>> replyTasks = new();

    public List<Task<TReply>> GetReplyTasks() => replyTasks;
    public void AddReplyTask(Task<TReply> replyTask) => replyTasks.Add(replyTask);
    public void RemoveReplyTask(Task<TReply> replyTask) => replyTasks.Remove(replyTask);

    public GrpcChannel? CreateAndStartChannel()
    {
        try
        {
            var handler = new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) => true //probably not the best idea, but for demo purposes
                }
            };

            Channel = GrpcChannel.ForAddress($"https://{GetServerIp()}:{GetServerPort()}", new GrpcChannelOptions
            {
                HttpHandler = handler
            });

            Channel.ConnectAsync().Wait();

            Client = Activator.CreateInstance(typeof(TClient), Channel) as TClient
                ?? throw new InvalidOperationException($"{typeof(TClient).Name} could not be instantiated");


            GetLogger().LogInformation($"Connected to {GetServiceName()}");
        }
        catch (Exception ex)
        {
            GetLogger().LogError(ex, $"Error connecting to {GetServiceName()}");
        }

        return Channel;
    }

    public void ProcessReplys()
    {
        Task.Run(() => ProcessMessagesAsync(GetCancellationTokenSource().Token));
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!replyTasks.Any())
            {
                await Task.Delay(100);
                continue;
            }

            var completedTask = await Task.WhenAny(replyTasks);
            if (completedTask.IsCompletedSuccessfully)
            {
                var reply = await completedTask;
                ProcessReply(reply);
                replyTasks.Remove(completedTask);
            }
            else if (completedTask.IsFaulted)
            {
                GetLogger().LogError(completedTask.Exception, "Error processing reply.");
                replyTasks.Remove(completedTask);
            }
        }
    }

    public void Dispose()
    {
        Channel?.Dispose();
        Channel = null;
        GetLogger().LogInformation($"Disconnected from {GetServiceName()}");
    }

    public void OnClose()
    {
        GetCancellationTokenSource().Cancel();
        _ = Channel?.ShutdownAsync();
        Dispose();
        GetLogger().LogInformation($"Closed {GetServiceName()} service.");
    }

    // Methods that must be implemented by subclasses
    public abstract ILogger GetLogger();
    public abstract string GetServiceName();
    public abstract string GetServerIp();
    public abstract int GetServerPort();
    public abstract CancellationTokenSource GetCancellationTokenSource();
    public abstract void ProcessReply(TReply reply);
}
