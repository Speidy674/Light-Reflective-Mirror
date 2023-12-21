// wraps Telepathy for use as HLAPI TransportLayer
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

// Replaced by Kcp November 2020
namespace Mirror
{
    public class TelepathyTransport : Transport, PortTransport
    {
        // scheme used by this transport
        // "tcp4" means tcp with 4 bytes header, network byte order
        public const string Scheme = "tcp4";

        public ushort port = 7777;
        public ushort Port { get => port; set => port=value; }

        public bool NoDelay = true;
        public int SendTimeout = 5000;
        public int ReceiveTimeout = 30000;

        public int serverMaxMessageSize = 16 * 1024;
        public int serverMaxReceivesPerTick = 10000;
        public int serverSendQueueLimitPerConnection = 10000;
        public int serverReceiveQueueLimitPerConnection = 10000;

        public int clientMaxMessageSize = 16 * 1024;
        public int clientMaxReceivesPerTick = 1000;
        public int clientSendQueueLimit = 10000;
        public int clientReceiveQueueLimit = 10000;

        Telepathy.Client client;
        Telepathy.Server server;

        // scene change message needs to halt  message processing immediately
        // Telepathy.Tick() has a enabledCheck parameter that we can use, but
        // let's only allocate it once.
        Func<bool> enabledCheck;

        public new void Awake()
        {
            TelepathyConfig conf = new TelepathyConfig();
            if (!File.Exists("TelepathyConfig.json"))
            {
                File.WriteAllText("TelepathyConfig.json", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            else
            {
                conf = JsonConvert.DeserializeObject<TelepathyConfig>(File.ReadAllText("TelepathyConfig.json"));
            }

            NoDelay = conf.NoDelay;
            SendTimeout = conf.SendTimeout;
            ReceiveTimeout = conf.ReceiveTimeout;

            serverMaxMessageSize = conf.serverMaxMessageSize;
            serverMaxReceivesPerTick = conf.serverMaxReceivesPerTick;
            serverSendQueueLimitPerConnection = conf.serverSendQueueLimitPerConnection;
            serverReceiveQueueLimitPerConnection = conf.serverReceiveQueueLimitPerConnection;

            // allocate enabled check only once
            enabledCheck = () => true;

            Console.WriteLine("TelepathyTransport initialized!");
        }

        public override bool Available()
        {
            // C#'s built in TCP sockets run everywhere except on WebGL
            return true;
        }

        // client
        private void CreateClient()
        {
            // create client
            client = new Telepathy.Client(clientMaxMessageSize);
            // client hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            client.OnConnected = () => OnClientConnected.Invoke();
            client.OnData = (segment) => OnClientDataReceived.Invoke(segment, Channels.Reliable);
            // fix: https://github.com/vis2k/Mirror/issues/3287
            // Telepathy may call OnDisconnected twice.
            // Mirror may have cleared the callback already, so use "?." here.
            client.OnDisconnected = () => OnClientDisconnected?.Invoke();

            // client configuration
            client.NoDelay = NoDelay;
            client.SendTimeout = SendTimeout;
            client.ReceiveTimeout = ReceiveTimeout;
            client.SendQueueLimit = clientSendQueueLimit;
            client.ReceiveQueueLimit = clientReceiveQueueLimit;
        }
        public override bool ClientConnected() => client != null && client.Connected;
        public override void ClientConnect(string address)
        {
            CreateClient();
            client.Connect(address, port);
        }

        public override void ClientConnect(Uri uri)
        {
            CreateClient();
            if (uri.Scheme != Scheme)
                throw new ArgumentException($"Invalid url {uri}, use {Scheme}://host:port instead", nameof(uri));

            int serverPort = uri.IsDefaultPort ? port : uri.Port;
            client.Connect(uri.Host, serverPort);
        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            client?.Send(segment);

            // call event. might be null if no statistics are listening etc.
            OnClientDataSent?.Invoke(segment, Channels.Reliable);
        }
        public override void ClientDisconnect()
        {
            client?.Disconnect();
            client = null;
            // client triggers the disconnected event in client.Tick() which won't be run anymore
            OnClientDisconnected?.Invoke();
        }

        // messages should always be processed in early update
        public override void ClientEarlyUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!true) return;

            // process a maximum amount of client messages per tick
            // IMPORTANT: check .enabled to stop processing immediately after a
            //            scene change message arrives!
            client?.Tick(clientMaxReceivesPerTick, enabledCheck);
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = port;
            return builder.Uri;
        }
        public override bool ServerActive() => server != null && server.Active;
        public override void ServerStart()
        {
            // create server
            server = new Telepathy.Server(serverMaxMessageSize);

            // server hooks
            // other systems hook into transport events in OnCreate or
            // OnStartRunning in no particular order. the only way to avoid
            // race conditions where telepathy uses OnConnected before another
            // system's hook (e.g. statistics OnData) was added is to wrap
            // them all in a lambda and always call the latest hook.
            // (= lazy call)
            server.OnConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
            server.OnData = (connectionId, segment) => OnServerDataReceived.Invoke(connectionId, segment, Channels.Reliable);
            server.OnDisconnected = (connectionId) => OnServerDisconnected.Invoke(connectionId);

            // server configuration
            server.NoDelay = NoDelay;
            server.SendTimeout = SendTimeout;
            server.ReceiveTimeout = ReceiveTimeout;
            server.SendQueueLimit = serverSendQueueLimitPerConnection;
            server.ReceiveQueueLimit = serverReceiveQueueLimitPerConnection;

            server.Start(port);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            server?.Send(connectionId, segment);

            // call event. might be null if no statistics are listening etc.
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }
        public override void ServerDisconnect(int connectionId) => server?.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId)
        {
            try
            {
                return server?.GetClientAddress(connectionId);
            }
            catch (SocketException)
            {
                // using server.listener.LocalEndpoint causes an Exception
                // in UWP + Unity 2019:
                //   Exception thrown at 0x00007FF9755DA388 in UWF.exe:
                //   Microsoft C++ exception: Il2CppExceptionWrapper at memory
                //   location 0x000000E15A0FCDD0. SocketException: An address
                //   incompatible with the requested protocol was used at
                //   System.Net.Sockets.Socket.get_LocalEndPoint ()
                // so let's at least catch it and recover
                return "unknown";
            }
        }
        public override void ServerStop()
        {
            server?.Stop();
            server = null;
        }

        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!true) return;

            // process a maximum amount of server messages per tick
            // IMPORTANT: check .enabled to stop processing immediately after a
            //            scene change message arrives!
            server?.Tick(serverMaxReceivesPerTick, enabledCheck);
        }

        // common
        public override void Shutdown()
        {
            Console.WriteLine("TelepathyTransport Shutdown()");
            client?.Disconnect();
            client = null;
            server?.Stop();
            server = null;
        }

        public override int GetMaxPacketSize(int channelId)
        {
            return serverMaxMessageSize;
        }

        public override string ToString()
        {
            if (server != null && server.Active && server.listener != null)
            {
                // printing server.listener.LocalEndpoint causes an Exception
                // in UWP + Unity 2019:
                //   Exception thrown at 0x00007FF9755DA388 in UWF.exe:
                //   Microsoft C++ exception: Il2CppExceptionWrapper at memory
                //   location 0x000000E15A0FCDD0. SocketException: An address
                //   incompatible with the requested protocol was used at
                //   System.Net.Sockets.Socket.get_LocalEndPoint ()
                // so let's use the regular port instead.
                return $"Telepathy Server port: {port}";
            }
            else if (client != null && (client.Connecting || client.Connected))
            {
                return $"Telepathy Client port: {port}";
            }
            return "Telepathy (inactive/disconnected)";
        }
    }
}
