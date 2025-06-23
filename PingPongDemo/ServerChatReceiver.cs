using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using MCGateway;
using MCGateway.Protocol;
using JTJabba.EasyConfig;
using System.Threading.Tasks;
using System.Net.Security;
using MCGateway.Protocol.Versions.P759_G1_19;
using PingPongDemo.InterceptionServices;
using PingPongDemo.InterceptionServices.Services;

namespace PingPongDemo
{
    public class ServerChatReceiver 
    {

        Guid _id; 
        ServiceManager _serviceManager;
        ChatService _chatService;
        /*
        public static void Initialize()
        {
          

            string serverIp = Config.Services.ChatService.IP; 
            int serverPort = Config.Services.ChatService.Port; 

            try
            {
                var handler = new SocketsHttpHandler
                {
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                    }
                };

                Channel = GrpcChannel.ForAddress($"https://{serverIp}:{serverPort}", new GrpcChannelOptions
                {
                    HttpHandler = handler
                });

                Channel.ConnectAsync().Wait();
                Client = new Messager.MessagerClient(Channel);

                Logger.LogInformation("Connected to Chat Service");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred while connecting to the Chat Service");
            }

            //begin the reply confirmation task
            Task.Run(() => ProcessMessagesAsync(cancellationTokenSource.Token));

        }*/

        public ServerChatReceiver(Guid id, ServiceManager serviceManager)
        {
            _id = id;
            _serviceManager = serviceManager;
            _chatService = serviceManager.GetServiceFromRegistry<ChatService>();

        }

        public void AcceptChatMessage(Packet packet)
        {
            _chatService.AcceptChatMessagePacket(packet, _id);
        }

        /*

          public void Dispose()
          {

          }

          public void AcceptChatMessagePacket(Packet packet)
          {
              string message = packet.ReadString();
              Logger.LogInformation(message);

              var reply = Client?.SendMessageAsync(new MessageRequest { Uuid = _id.ToString(), Message = message });

              if (reply == null)
              {
                  Logger.LogError("Reply received was null");
                  return;
              }

              replyTasks.Add(reply.ResponseAsync);
          }

          public static async Task ProcessMessagesAsync(CancellationToken cancellationToken)
          {
              while (!cancellationToken.IsCancellationRequested)
              {
                  if (!replyTasks.Any())
                  {
                      await Task.Delay(100); // Wait for a short while before checking again.
                      continue;
                  }

                  Task<MessageConfirmation> completedTask = await Task.WhenAny(replyTasks);

                  if (completedTask.IsCompletedSuccessfully)
                  {
                      MessageConfirmation reply = await completedTask;
                      ProcessReply(reply);
                      replyTasks.Remove(completedTask); // Remove the specific completed task
                  }
                  else if (completedTask.IsFaulted)
                  {
                      Logger.LogError(completedTask.Exception, "Error processing reply.");
                      replyTasks.Remove(completedTask); // Remove the specific faulted task
                  }
              }
          }


          private static void ProcessReply(MessageConfirmation reply)
          {
              Logger.LogInformation("Received reply: " + reply.Status);
          }

          public static void Close()
          {
              cancellationTokenSource.Cancel();
              Channel?.ShutdownAsync();
          }
        */
    }
}
