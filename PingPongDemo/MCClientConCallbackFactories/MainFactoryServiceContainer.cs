using MCGateway;
using PingPongDemo.InterceptionServices;

namespace PingPongDemo.MCClientConCallbackFactories
{
    internal class MainFactoryServiceContainer
    {
        public PingPongService PingPongService_ { get; }

        public MainFactoryServiceContainer(ConnectionsDictionary connectionsDict)
        {
            PingPongService_ = new(connectionsDict);
        }
    }
}
