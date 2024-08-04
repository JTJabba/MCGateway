using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.ChatService
{
    internal class ClientChatMessagerService : ClientChatMessager.ClientChatMessagerBase
    {

        public override Task<ClientChatMessageConfirmation> ReceiveMessage(ClientChatMessageRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ClientChatMessageConfirmation()
            {
                Status = "received"
            });
        }
    }
}
