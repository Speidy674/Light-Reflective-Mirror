![Logo](LRM.png)

# Light Reflective Mirror

![GitHub issues](https://img.shields.io/github/issues-raw/Speidy674/Light-Reflective-Mirror)

[![Build Node](https://github.com/Speidy674/Light-Reflective-Mirror/actions/workflows/build-node.yml/badge.svg)](https://github.com/Speidy674/Light-Reflective-Mirror/actions/workflows/build-node.yml)
[![Build Load Balancer](https://github.com/Speidy674/Light-Reflective-Mirror/actions/workflows/build-loadbalancer.yml/badge.svg)](https://github.com/Speidy674/Light-Reflective-Mirror/actions/workflows/build-loadbalancer.yml)

## What

Light Reflective Mirror is a transport for Mirror Networking which relays network traffic through your own servers. This allows you to have clients host game servers and not worry about NAT/Port Forwarding, etc. 

## Features
* WebGL Support, WebGL can host servers!
* Built in server list!
* Relay password to stop other games from stealing your precious relay!
* Relay supports connecting users without them needing to port forward!
* NAT Punchtrough (Full Cone, Restricted Cone, and Port Restricted Cone)
* Direct Connecting
* Load Balancing with multi-relay setup

## NAT punch and direct connect

Peers that can punch talk to each other directly, and the relay is used only for
matchmaking. Peers that cannot fall back to relaying through the node.

To enable it, set `useNATPunch` on the transport and add an `LRMDirectConnectModule`
with a `KcpTransport` assigned as its direct connect transport. The relay node needs
`EnableNATPunchtroughServer` in its `config.json`.

Symmetric NAT cannot be punched and always relays. If a direct connection fails for
any other reason the client falls back to the relay and still joins. Set
`Show Debug Logs` on the direct connect module to see which path a client took.

## Requirements

- Unity 2021.3 or newer, with Mirror 93.0.0 or newer.
- .NET 8 runtime for the relay node and the load balancer.

V14 clients cannot connect to a V16 relay, and V16 clients cannot connect to a V14
relay. Coming from V14, the relay and every shipped client have to be updated
together. The [release notes](https://github.com/Speidy674/Light-Reflective-Mirror/releases)
list what changed.

## Tutorials

(I recommend these over the text format)

### How to setup LRM on an ubuntu server
https://www.youtube.com/watch?v=0SpKIs0Beuo

### How to setup LRM in unity, along with basic usage
https://www.youtube.com/watch?v=Wi0rp2b8KmM

## Usage

First things first, you will need:
* Mirror, Install that from Asset Store.
* Download the latest release of Light Reflective Mirror Unity Package and put that in your project also. Download from: [Releases](https://github.com/Speidy674/Light-Reflective-Mirror/releases).

#### Client Setup
Attach the LightReflectiveMirrorTransport script to your NetworkManager and set it as the NetworkManager's Transport. Under the transport's **LRM Settings** tab, put in the IP/Port of your relay server.

Then attach the transport LRM should use to reach the relay (`KcpTransport` to match the default relay config, or `SimpleWebTransport` if you need WebGL) and assign it to the **LRM Transport** field (the `clientToServerTransport` variable). This must match the relay's `TransportClass`; see [Server Config](#server-config) below.

When you start a server, you can simply get the URI from the transport and use that to connect. If you wish to connect without the URI, the LightReflectiveMirror component has a public "Server ID" field which is what clients would set as the address to connect to. 

If your relay server has a password, enter it in the **LRM Auth Key** field (the `authenticationKey` variable) under the **LRM Settings** tab of the transport inspector, or you wont be able to connect. By default the relays have the password as "Secret Auth Key". Change this on both the relay and the client before exposing a relay publicly, or anyone can use it.

##### Server List

Light Reflective Mirror has a built in room/server list if you would like to use it. To use it you need to set the server values (Server Name, Extra Server Data, Max Players, Is Public Server) under the **Other** tab in the transport inspector. Also if you would like to make the server show on the list, make sure "Is Public Server" is checked. Once you create a server, you can update those variables from the "UpdateRoomInfo" function on the LightReflectiveMirrorTransport script.

> **`Max Players` counts the host.** `Max Players` 4 means the host plus 3 clients.
> A dedicated server (`NetworkManagerMode.ServerOnly`) is not a player, so in that
> mode `Max Players` 4 means 4 clients.

To request the server list you need a reference to the LightReflectiveMirrorTransport from your script and call 'RequestServerList()'. This will invoke a request to the server to update our server list. Once the response is recieved the field 'relayServerList' will be populated and you can get all the servers from there.
 
#### Server Setup
Download the latest Server release from: [Releases](https://github.com/Speidy674/Light-Reflective-Mirror/releases)
Make sure you have .NET 8.0 Runtime
And all you need to do is run LRM.exe on windows, or "dotnet LRM.dll" on linux!

#### Server Config
In the config.json file there are a few fields.

TransportClass - The fully-qualified class name of the transport inside `MultiCompiled.dll`, including its namespace. There are 3 compiled transports available:

| Value | Protocol |
|---|---|
| `kcp2k.KcpTransport` | UDP (default) |
| `Mirror.SimpleWebTransport` | WebSockets, required for WebGL |
| `MultiCompiled.KcpWebCombined` | Both, on separate ports |

**The transport you set here must match the transport you assign as `ClientToServerTransport`
in Unity.** A KCP relay will not answer a SimpleWeb client, and the failure is silent:
the client simply never connects. The default on both the relay and the Docker image is
`kcp2k.KcpTransport`, so if you follow the WebGL/SimpleWeb setup above, change this too.

> Note: the class is `Mirror.SimpleWebTransport`, **not** `Mirror.SimpleWeb.SimpleWebTransport`.
> The relay's compiled copy sits in the `Mirror` namespace, unlike the Unity-side script.

AuthenticationKey - This is the key the clients need to have on their inspector. It cannot be blank.

RandomlyGeneratedIDLength - The number of characters in a room code. Codes use uppercase A-Z.

UpdateLoopTime - The time in miliseconds between calling 'Update' on the transport

UpdateHeartbeatInterval - the amounts of update calls before sending a heartbeat. By default its 100, which if updateLoopTime is 10, means every (10 * 100 = 1000ms) it will send out a heartbeat.

## Compatibility Matrix

| Component | Original LRM | Speidy674 V14 | This Release (V16) |
|-----------|--------------|---------------|--------------------|
| .NET (relay node) | 5.0 | 7.0 | 8.0 |
| .NET (load balancer) | 5.0 | 7.0 | 8.0 |
| Mirror | Up to ~30.x | 86.13.4 | 93.0.0 |
| Unity | 2020.3+ | 2021.3+ | 2021.3+ |

## What to choose, Epic, Steam, LRM?

There are quiet a few relay transports for mirror at this point, It can often be difficult to pick one that most suits your needs. So I'll quickly go over my view on it and hopefully it helps you make an informed decision.

### Steam
Starting with steam, steam offers a free relay with NAT punchthrough for anyone releasing a game on steam. This integrates into their lobby invites and also only allows connections from other users who actually own the game (No pirates sneaking into your servers!) and it works wonders. Steam has well documented SDK, a huge community, and they are active on their forums. If you plan on releasing on steam and only steam, go with this. To get the steam relay, go into the #steam channel in mirror's discord and use whichever one is the same as your wrapper.

### Epic
Epic is a newer transport that offers NAT Punchthrough, and a relay service for free. As of writing this its only available for usage on Windows/Mac/Linux (More platforms are planned and releasing in the future). This one is great because they offer it for free! Thats right, a free relay and NAT punchthrough server, plus more! They have more tools such as Matchmaking, server browser, statistics, and more! This is NOT locked into only releasing on Epic Store, like how steams is. So you can release on any store you want if your game uses this. Now onto the downsides, they have a very PITA SDK to use with a fairly small community for the C# side of things. (FakeByte helps alot in the discord and will help with features outside of the relay transport!). The documentation is sub-par and severely lacking in some places, which is expected as its fairly new. They also have Epic Account Services, which is similar to steams but like the relay, not locked into one store! With those services you get user accounts, In game purchases, achievements, and much more. So if you want a free relay/NAT Punchthrough server, and want to go along for the ride of EoS, this is the one. You cant beat free. :P Check it out [here](https://github.com/FakeByte/EpicOnlineTransport)

### LRM
LRM is a self-hosted, open source, relay/NAT Punchthrough server. It's available for all platforms (PC, Mac, Linux, WebGL, Android, IOS, You name it!). It does this by supporting any of mirrors existing transports. If you want webgl? Use websockets! UDP? KCP! This is one of LRM's main features. The game developer can decide on how they want their data sent between the server and clients. With LRM, you are going to have to host the servers yourself. A load balancer ships alongside the relay (see `LoadBalancerProject`), which makes it easy to expand servers in regions and balance users out between them. The more powerful of a server you have, the more that LRM node can host. With some tests (All clients relayed, none NAT punched), we could get about ~200 CCU on a $5 google cloud server (f1-micro). **V16 keeps LRM working with current Mirror and .NET releases.** So, if you are more of a self-hosting person, who wants full control of your servers, or want a relay for a platform the others don't support (WebGL). Use LRM, if you have any questions, we are in the discord channel everyday! :)

## Credits

**Maintenance Chain:**
* **Derek-R-S** - Original creator and maintainer through v12
* **Speidy674** - Community maintenance fork, V14 with .NET 7 upgrade  
* **Biebras** - V14 bug fixes and improvements
* **jaredbrandjes** - V15/V16 Mirror compatibility and .NET 8 modernization

**Original Contributors:**
* **Cooper** - Assisted with development and made some wonderful features! He's also active in the discord to help answer questions and help with issues.
* **Maqsoom & JesusLuvsYooh** - Both really active testers and have been testing it since the idea was pitched. They tested almost all versions of DRM and LRM!
* **All Mirror Transport Creators!** - They made all the transports that this thing relies on! Especially the Simple Web Transport by default!

## Project History

- **Original**: [Derek-R-S/Light-Reflective-Mirror](https://github.com/Derek-R-S/Light-Reflective-Mirror) (v1-v12)
- **V14 Base**: [Speidy674/Light-Reflective-Mirror](https://github.com/Speidy674/Light-Reflective-Mirror) (community maintenance)
- **V15**: [jaredbrandjes/Light-Reflective-Mirror](https://github.com/jaredbrandjes/Light-Reflective-Mirror) (Mirror 93, .NET 8)
- **V16**: this repository, co-maintained by Speidy674 and jaredbrandjes

## License
[MIT](https://choosealicense.com/licenses/mit/)