<img src="https://imgur.com/s0x4f8U.jpg" width="64" height="64" style="display: inline-block; vertical-align: middle;" alt="Description" />
<h1 style="display: inline-block; margin-left: 10px; line-height: 64px;">MCGateway</h1>

*Like if BungeeCord was a high-performance modding framework*

MCGateway is a Minecraft network programming platform, providing infrastructure for building high-performance proxy servers with custom packet intercepters and translators between a client and server connection, and performing packet injection from external services. It is designed for traffic-layer modding to offload tasks from the main server that are well suited to being implemented at the traffic layer.

Gateway can help offload:
 - any, but especially lag intensive plugins, that don't interact with the server state in complex ways
 - replay systems
 - things that spawn temporary entities like particles and fireworks
 - auction houses
 - chat systems
 - certain anticheat
 - vanilla functions like sending chunk data from unloaded chunks to boost render distance with no server-side performance penalty (it just needs a somewhat recent copy of world data)
 - vanilla functions like propagating player position and rotation data

The last two examples are easy ways to dramatically reduce packet and data throughput requirements on a server, and additional simple optimizations combined with running many mods on Gateway could allow heavily modded servers to support thousands of players concurrently in one world. The primary motivation behind Gateway's creation is to enable MMOs of insane scale with no sacrifices.

Any mods written on Gateway will work regardless of the backend jar or server implementation.

## Usage and Architecture
The first thing to know is components of Gateway take callbacks, like `IGatewayConnectionCallback` and `IMCClientConnectionCallback`, which provides control over Gateway. The callbacks require methods for doing things like getting a ping response and adding and disconnecting players.

Look at [TestGateway1/MCClientConCallback](https://github.com/JTJabba/MC-Gateway/blob/90d84baa37ca6e4b054644a0157f8ac2d06fd723/TestGateway1/MCClientConCallback.cs). The client connection  will get a stateful callback using `GetCallback`, so you can implement stateful logic to control the connection. The client will forward its serverbound traffic through the callback with `Forward`, and you can do whatever you want with this. This callback will get a server connection to `192.168.1.2:25565` and pass `clientBoundReceiver` as the thing for the server to forward its clientbound traffic to, which is the client connection. When the client connection forwards its traffic to the callback, it simply calls `_serverBoundReceiver::forward` where `_serverBoundReceiver` is the server connection. You can make a packet intercepter by substituting a custom `IServerboundReceiver` into `_serverBoundReceiver` that does its thing before calling forward on the server connection:
```cs
var serverConnection = new MCServerConnection(serverClient, username, uuid, GetTranslationsObject(), clientBoundReceiver);

var intercepter = new MyIntercepter(forwardTo: serverConnection)

_serverBoundReceiver = intercepter;
```
The same thing can be done for the `IClientBoundReceiver` passed to the server connection (`clientBoundReceiver`).


The main entrypoint for actually running Gateway is `MCGateway/Gateway.cs`, which acts as a TCP server and tries to spin off incoming connections into `GatewayConnection`'s. `GatewayConnection` wraps the TCP connection and only has a high-level concept of the Minecraft protocol. It will call `EarlyConnectionHandler::TryHandleTilLogin` which will walk through the early connection cycle of all Minecraft connections, fulfilling ping requests and triggering a disconnect or returning successfully upon reaching the login phase. Upon `TryHandleTilLogin` returning successfully, `GatewayConnection` will pass the handshake information to its callback to get an instance of it. It will then use its callback to get a logged in client connection. This will be a type implementing the version-agnostic `IMCClientConnection` interface and the callback will usually use a version-specific type from Gateway that matches the version reported in the handshake information (see [TestGateway1/GatewayConCallback](https://github.com/JTJabba/MC-Gateway/blob/90d84baa37ca6e4b054644a0157f8ac2d06fd723/TestGateway1/GatewayConCallback.cs)).

## Current Development State
Gateway is still very much in development and user-facing interfaces and components will likely change or move around a bit.

It is currently possible to use Gateway with 1.19 (V759) and write low-level intercepters on it, but it is still missing several abstraction layers before it will be a mature traffic-layer modding framework.

These are the current next milestones of development:
 - JTJabba.EasyConfig needs to be updated to support multi-project solutions and more complex configurations. New version is having dependancy handling issues and won't run in Visual Studio... will come back to later.
 - Need to add support for querying `Gateway` objects for connections and injecting traffic into them.
 - Need to move code out of MCGateway.Protocol.V759 that can be shared by version specific implementations, and refactor to prepare for supporting every version. 
 - Compatibility layer for writing strongly-typed multi-version intercepters:
   -  In a json, track new/changed/deleted packets every version
   -  Define all unique packets (packets not compatible with ones from previous versions) (ex. `[PacketDefinition(Id = 0, Version = 47)] public ref struct V47_Id0_KeepAlive { ... }`)
   -  Source generator generates unified namespaces for each version ( ex. future version: `namespace MCGateway.Protocols.V49.Packets.Clientbound { using KeepAlive = MCGateway.Protocol.Packets.Clientbound.V47_Id0_KeepAlive; ... }`)
 - Source generator to automatically generate `Forward` methods for all versions compatible with a receiver and auto-route packets to their intercept methods (see [this concept](https://github.com/JTJabba/MC-Gateway/blob/90d84baa37ca6e4b054644a0157f8ac2d06fd723/TestGateway1/Receiver.cs) of roughly what I'm hoping to do). Will also need to combine routing of multiple chained receivers so they don't have to go through many switchs somehow...
