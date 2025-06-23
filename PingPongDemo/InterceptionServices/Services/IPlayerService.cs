using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.Services
{
 
    public interface IPlayerService : IService
    {
        public Task OnPlayerJoin(Guid playerUuid, string playerName);

        public Task OnPlayerLeave(Guid playerUuid, string playerName);
    }
}
