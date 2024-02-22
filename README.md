<img src="https://imgur.com/s0x4f8U.jpg" width="64" height="64" style="display: inline-block; vertical-align: middle;" alt="Description" />
<h1 style="display: inline-block; margin-left: 10px; line-height: 64px;">MCGateway</h1>

*Like if BungeeCord was a high-performance modding framework*

MCGateway is a Minecraft network programming platform, providing infrastructure for building high-performance proxies with custom packet interception, injection, and translation logic between a client and server connection. It is designed for traffic-layer modding to offload tasks from the main server that are well suited to being implemented at the traffic layer.

Gateway can help offload:
 - any, but especially lag intensive plugins, that don't interact with the server state in complex ways
 - replay systems
 - things that spawn temporary entities like particles
 - auction houses
 - chat systems
 - certain anticheat
 - vanilla functions like sending chunk data from unloaded chunks to boost render distance with no server-side performance penalty (it just needs a somewhat recent copy of world data)
 - vanilla functions like propagating player position and rotation data

The last two examples are easy ways to dramatically reduce packet and data throughput requirements on a server, and additional simple optimizations combined with running many mods on Gateway could allow heavily modded servers to support thousands of players concurrently in one world.

Any mods written on Gateway will work regardless of the backend jar or server implementation.

## Usage and Architecture
### Overview
Currently the focus is to provide components for building custom proxies instead of a runnable implementation with a modding API. [PingPongDemo](https://github.com/JTJabba/MCGateway/tree/master/PingPongDemo) is easily modifiable and should serve as a good starting point. 

The highest-level component is [Gateway](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Gateway.cs), which is acts as a TCP server and spins off [GatewayConnections](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/GatewayConnection.cs). Gateway requires an [IGatewayConnectionCallback](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/IGatewayConnectionCallback.cs), which is where you hook custom logic into Gateway, and a [ConnectionsDictionary](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/ConnectionsDictionary.cs). The ConnectionsDictionary is just an abstracted `Dictionary<Guid, GatewayConnection>` that is treated as its own service because it needs to be injected into Gateway and anything needing to look up connections that shouldn't directly reference Gateway.
```cs
// Gateway's constructor
Gateway(IGatewayConnectionCallback callback, ConnectionsDictionary connectionDict, ILoggerFactory? loggerFactory = null)
```

A [GatewayConnection](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/GatewayConnection.cs) wraps the underlying TCP connection, and manages the state of the Minecraft connection through the login process. Once the connection is logged in, it requests an [IMCClientConnection](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/IMCClientConnection.cs) from the callback:
```cs
clientCon = callback.GetLoggedInClientConnection(handshake, tcpClient);
```
MCGateway provides `MCClientConnection` and `MCServerConnection` implementations for different versions which can be chosen based off the handshake (contains client version). You can also use custom implementations. Currently, only 1.19 (protocol version 759) is implemented, with plans to do 1.8+.

`MCClientConnection`s from each version take a callback factory that will return callback instances, for V759 [IMCClientConCallbackFactory](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/IMCClientConCallbackFactory.cs) and [IMCClientConCallback](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/IMCClientConCallback.cs). **Packets are routed through this callback, and this is where packet interception logic can be introduced.**

### PingPongDemo
Let's go through the services started in [Program.cs](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/Program.cs):
```cs
services.AddSingleton<ConnectionsDictionary>();
services.AddSingleton<IMCClientConCallbackFactory, MC4FactoryMain>();
services.AddSingleton<IGatewayConnectionCallback, GatewayConCallback>();
services.AddSingleton<IGateway, Gateway>();
services.AddHostedService<Worker>();
```

- `ConnectionsDictionary` is the abstracted `Dictionary<Guid, GatewayConnection>` defined in MCGateway. It will be injected into `Gateway`, and `MC4FactoryMain` which injects it into its service provider.
- `MC4FactoryMain` is the factory that creates `MCClientConCallback`'s for the [MCClientConnection](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/MCClientConnection.cs) from MCGateway's V759 (1.19) protocol library. It encapsulates logic for routing, traffic manipulation, and related services.
- `GatewayConCallback` provides the callback needed by `Gateway`. It includes things like getting a status response, adding/removing online players, and getting an object to represent post-login minecraft connections. We use [MCClientConnection](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/MCClientConnection.cs) which is what needs [MC4FactoryMain](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/MCClientConCallbackFactories/MC4FactoryMain.cs).
- `Gateway` is the object representing the proxy, and has `GatewayConCallback` and `ConnectionsDictionary` injected into it.
- `Worker` is the hosted service, which manages `Gateway`'s lifecycle.

### Packet Interception & Injection
`MCClientConnection`s from MCGateway's protocol libraries forward their traffic through their callback:
```cs
public Task ReceiveTilClosedAndDispose()
{
    _callback.StartedReceivingCallback();
    return Task.Run(() =>
    {
        try
        {
            while (true)
            {
                using var packet = ReadPacket();
                _callback.Forward(packet);
    ...
```

They also implement [IClientboundReceiver](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/IClientboundReceiver.cs) for their corresponding version, and clientbound packets can be forwarded using `Forward`:
```cs
public void Forward(Packet packet)
{
    WritePacket(packet);
}
```

`MCServerConnection`s implement [IServerboundReceiver](https://github.com/JTJabba/MCGateway/blob/master/MCGateway/Protocol/Versions/P759_G1_19/IServerboundReceiver.cs), but don't need a callback and just take a `IClientboundReceiver` to forward to.

You can write your own receivers as well, like [PingPongReceiver](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/ServerboundReceivers/PingPongReceiver.cs):
```cs
internal sealed class PingPongReceiver : IServerboundReceiver
{
    // fields

    public PingPongReceiver(IPingPongService service, Guid clientUuid, IServerboundReceiver forwardTo)
    { ... }

    public void Forward(Packet packet)
    {
        switch (packet.PacketID)
        {
            case 0x4: // Chat packet ID in V759 (1.19)
                // Intercept it if it contains "ping", otherwise forward it
                if (!TryInterceptPing(packet)) _receiver.Forward(packet);
                break;
            default:
                _receiver.Forward(packet);
                break;
        }
    }

    bool TryInterceptPing(Packet packet)
    {
        var msg = packet.ReadString();
        if (msg != "ping") return false;

        // After parsing relevant info from packet stream,
        // send to service that implements any logic that can
        // be decoupled from the transmission protocol
        _service.PingReceived(_clientUuid);
        _logger.LogDebug("PingPongReceiver intercepted ping");
        return true;
    }
```

Most receivers will want to take a `forwardTo` parameter to continue passing traffic. Receivers can then be chained together. PingPongDemo's serverbound packet flow looks like this:
```
MCClientConnection -> MCClientConCallback -> PingPongReceiver -> MCServerConnection
```

In PingPongDemo, clientbound packets flow directly from the server connection to the client connection, but a receiver chain can be built and used as the receiver instead. You can look at [MC4FactoryMain](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/MCClientConCallbackFactories/MC4FactoryMain.cs) to see how it starts a server connection, makes it forward to the client connection, and makes the client callback forward through the receiver to the server connection.

### Scoping Notes

A receiver should only concern itself with one version and direction, and ONLY be concerned with parsing information from a version-specific packet stream to trigger calls to preferably version-agnostic services. In general, services should have no concept of specific packet types or anything related to the transmission protocol. [PingPongService](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/InterceptionServices/PingPongService.cs) breaks this rule and is version specific, directly handling traffic injection back toward the client. When multiversion support is added, this will be updated to demo a proper design pattern for injection.

The [MCClientConCallback](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/MCClientConCallback.cs) is responsible for forwarding serverbound packets. In PingPongDemo, a backend server connection is started and the receiver chains are built in [MC4FactoryMain](https://github.com/JTJabba/MCGateway/blob/master/PingPongDemo/MCClientConCallbackFactories/MC4FactoryMain.cs) and passed into the callback. However, the receiver chains should be rebuilt from scratch if the client switches backend servers. When I write an implementation with server switching, this logic will be moved under the callback itself.

## Current Development State
Gateway is still very much in development and user-facing interfaces and components will likely change or move around a bit.

It is currently possible to use Gateway with 1.19 (V759) and write low-level intercepters on it, but it is still missing several abstraction layers before it will be a mature traffic-layer modding framework.

These are the current next milestones of development:
 - JTJabba.EasyConfig needs to be updated to support multi-project solutions and more complex configurations. New version is having dependancy handling issues and won't run in Visual Studio... will come back to later.
 - Need to move code out of MCGateway.Protocol.V759 that can be shared by version specific implementations, and refactor to prepare for supporting every version. 
 - Compatibility layer for writing strongly-typed multi-version intercepters:
   -  In a json, track new/changed/deleted packets every version
   -  Define all unique packets (packets not compatible with ones from previous versions) (ex. `[PacketDefinition(Id = 0, Version = 47)] public ref struct V47_Id0_KeepAlive { ... }`)
   -  Source generator generates unified namespaces for each version ( ex. future version: `namespace MCGateway.Protocols.V49.Packets.Clientbound { using KeepAlive = MCGateway.Protocol.Packets.Clientbound.V47_Id0_KeepAlive; ... }`)
 - Source generator to automatically generate `Forward` methods for all versions compatible with a receiver and auto-route packets to their intercept methods (see [this concept](https://github.com/JTJabba/MC-Gateway/blob/90d84baa37ca6e4b054644a0157f8ac2d06fd723/TestGateway1/Receiver.cs) of roughly what I'm hoping to do). Will also need to combine routing of multiple chained receivers so they don't have to go through many switchs somehow...
