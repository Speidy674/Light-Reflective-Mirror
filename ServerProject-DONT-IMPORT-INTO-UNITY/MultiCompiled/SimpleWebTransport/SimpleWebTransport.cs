using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Security.Authentication;

namespace Mirror.SimpleWeb
{

    public class SimpleWebTransport : Transport, PortTransport
    {
        public const string NormalScheme = "ws";
        public const string SecureScheme = "wss";


        public ushort port = 7778;
        public ushort Port { get => port; set => port=value; }

        public bool ClientUseDefaultPort;

        public int maxMessageSize = 16 * 1024;

        public int handshakeMaxSize = 3000;

        public bool noDelay = true;

        public int sendTimeout = 5000;

        public int receiveTimeout = 20000;

        public int serverMaxMessagesPerTick = 10000;

        public int clientMaxMessagesPerTick = 1000;

        public bool batchSend = true;

        public bool waitBeforeSend = true;

        public bool clientUseWss;

        public bool sslEnabled;

        public string sslCertJson = "./cert.json";

        public SslProtocols sslProtocols = SslProtocols.Tls12;

        Log.Levels _logLevels = Log.Levels.warn;

        /// <summary>
        /// <para>Gets _logLevels field</para>
        /// <para>Sets _logLevels and Log.level fields</para>
        /// </summary>
        public Log.Levels LogLevels
        {
            get => _logLevels;
            set
            {
                _logLevels = value;
                Log.level = _logLevels;
            }
        }

        SimpleWebServer server;

        TcpConfig TcpConfig => new TcpConfig(noDelay, sendTimeout, receiveTimeout);

        public new void Awake()
        {
            Log.level = _logLevels;

            SWTConfig conf = new SWTConfig();
            if (!File.Exists("SWTConfig.json"))
            {
                File.WriteAllText("SWTConfig.json", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            else
            {
                conf = JsonConvert.DeserializeObject<SWTConfig>(File.ReadAllText("SWTConfig.json"));
            }

            ClientUseDefaultPort = conf.ClientUseDefaultPort;
            maxMessageSize = conf.maxMessageSize;
            handshakeMaxSize = conf.handshakeMaxSize;
            sendTimeout = conf.sendTimeout;
            noDelay = conf.noDelay;
            sendTimeout = conf.sendTimeout;
            receiveTimeout = conf.receiveTimeout;
            serverMaxMessagesPerTick = conf.serverMaxMessagesPerTick;
            clientMaxMessagesPerTick = conf.clientMaxMessagesPerTick;
            batchSend = conf.batchSend;
            waitBeforeSend = conf.waitBeforeSend;
            clientUseWss = conf.clientUseWss;
            sslEnabled = conf.sslEnabled;
            sslCertJson = conf.sslCertJson;
            sslProtocols = conf.sslProtocols;
    }

        void OnValidate()
        {
            Log.level = _logLevels;
        }

        public override bool Available() => true;

        public override int GetMaxPacketSize(int channelId = 0) => maxMessageSize;

        public override void Shutdown()
        {
            server?.Stop();
            server = null;
        }

        #region Client

        string GetClientScheme() => (sslEnabled || clientUseWss) ? SecureScheme : NormalScheme;

        public override bool ClientConnected() { return false; }

        public override void ClientConnect(string hostname) { }

        public override void ClientConnect(Uri uri) { }

        public override void ClientDisconnect() { }

        public override void ClientSend(ArraySegment<byte> segment, int channelId) { }

        public override void ClientEarlyUpdate() { }

        #endregion

        #region Server

        string GetServerScheme() => sslEnabled ? SecureScheme : NormalScheme;

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetServerScheme(),
                Host = Dns.GetHostName(),
                Port = port
            };
            return builder.Uri;
        }

        public override bool ServerActive()
        {
            return server != null && server.Active;
        }

        public override void ServerStart()
        {
            if (ServerActive())
                Console.Error.WriteLine("[SimpleWebTransport] Server Already Started");

            SslConfig config = SslConfigLoader.Load(sslEnabled, sslCertJson, sslProtocols);
            server = new SimpleWebServer(serverMaxMessagesPerTick, TcpConfig, maxMessageSize, handshakeMaxSize, config);

            server.onConnect += OnServerConnected.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, Channels.Reliable);
            server.onError += (connId, exception) => OnServerError(connId, TransportError.Unexpected, exception.ToString());

            SendLoopConfig.batchSend = batchSend || waitBeforeSend;
            SendLoopConfig.sleepBeforeSend = waitBeforeSend;

            server.Start(port);
        }

        public override void ServerStop()
        {
            if (!ServerActive())
                Console.Error.WriteLine("[SimpleWebTransport] Server Not Active");

            server.Stop();
            server = null;
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (!ServerActive())
                Console.Error.WriteLine("[SimpleWebTransport] Server Not Active");

            server.KickClient(connectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (!ServerActive())
            {
                Log.Error("[SimpleWebTransport] Server Not Active", false);
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SimpleWebTransport] Message greater than max size", false);
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SimpleWebTransport] Message count was zero", false);
                return;
            }

            server.SendOne(connectionId, segment);

            // call event. might be null if no statistics are listening etc.
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }

        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);

        public Request ServerGetClientRequest(int connectionId) => server.GetClientRequest(connectionId);

        // messages should always be processed in early update
        public override void ServerEarlyUpdate()
        {
            server?.ProcessMessageQueue();
        }

        #endregion
    }
}
