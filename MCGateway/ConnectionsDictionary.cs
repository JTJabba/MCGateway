using System.Collections.Concurrent;

namespace MCGateway
{
    /// <summary>
    /// Abstracted ConcurrentDictionary. Gateway and its dependencies need access to connections,
    /// so injecting it as its own service removes circular dependence and coupling with Gateway.
    /// </summary>
    public sealed class ConnectionsDictionary : ConcurrentDictionary<Guid, GatewayConnection> { }
}
