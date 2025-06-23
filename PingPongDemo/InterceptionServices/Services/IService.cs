using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingPongDemo.InterceptionServices.Services
{
    public interface IService
    {
        public ILogger GetLogger();
        public string GetServerIp();
        public int GetServerPort();

        public string GetServiceName();

        public CancellationTokenSource GetCancellationTokenSource();


        public void OnStart()
        {
           throw new NotImplementedException("OnStart method is not implemented.");
        }

        public void OnClose()
        {
            throw new NotImplementedException("OnClose method is not implemented.");
        }

    }
}
