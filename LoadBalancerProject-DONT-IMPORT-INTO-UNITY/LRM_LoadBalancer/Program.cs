﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LightReflectiveMirror.LoadBalancing
{
    class Program
    {
        /// <summary>
        /// Keeps track of all available relays.
        /// Key is server address, value is CCU.
        /// </summary>
        public Dictionary<RelayAddress, RelayStats> availableRelayServers = new();

        private int _pingDelay = 10000;
        const string API_PATH = "/api/stats";
        const string CONFIG_PATH = "config.json";

        public static Config conf;
        public static Program instance;

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            WriteTitle();
            instance = this;

            if (!File.Exists(CONFIG_PATH))
            {
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
                WriteLogMessage("A config.json file was generated. Please configure it to the proper settings and re-run!", ConsoleColor.Yellow);
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                conf = JsonConvert.DeserializeObject<Config>(File.ReadAllText(CONFIG_PATH));
                _pingDelay = conf.ConnectedServerPingRate;

                if (new EndpointServer().Start(conf.EndpointPort))
                    WriteLogMessage("Endpoint server started successfully", ConsoleColor.Green);
                else
                    WriteLogMessage("Endpoint server started unsuccessfully", ConsoleColor.Red);
            }

            var pingThread = new Thread(new ThreadStart(() => PingServers()));
            pingThread.Start();

            // keep console alive
            await Task.Delay(-1);
        }


        public async Task AddServer(string serverIP, ushort port, ushort endpointPort)
        {
            var stats = await ManualPingServer(serverIP, endpointPort);

            if(stats.HasValue)
                availableRelayServers.Add(new RelayAddress { Port = port, EndpointPort = endpointPort, Address = serverIP }, stats.Value);
        }

        public async Task<RelayStats?> ManualPingServer(string serverIP, ushort port) 
        {
            using (WebClient wc = new WebClient())
            {
                try
                {
                    string receivedStats = await wc.DownloadStringTaskAsync($"http://{serverIP}:{port}{API_PATH}");

                    return JsonConvert.DeserializeObject<RelayStats>(receivedStats);
                }
                catch(Exception e)
                {
                    // Server failed to respond to stats, dont add to load balancer.
                    return null;
                }
            }
        }

        async Task PingServers()
        {
            while (true)
            {
                WriteLogMessage("Pinging " + availableRelayServers.Count + " available relays");

                // Create a new list so we can modify the collection in our loop.
                var keys = new List<RelayAddress>(availableRelayServers.Keys);

                for(int i = 0; i < keys.Count; i++)
                {
                    string url = $"http://{keys[i].Address}:{keys[i].EndpointPort}{API_PATH}";

                    using (WebClient wc = new WebClient())
                    {
                        try
                        {
                            var serverStats = wc.DownloadString(url);
                            var deserializedData = JsonConvert.DeserializeObject<RelayStats>(serverStats);

                            WriteLogMessage("Server " + keys[i].Address + " still exists, keeping in collection.");

                            if (availableRelayServers.ContainsKey(keys[i]))
                                availableRelayServers[keys[i]] = deserializedData;
                            else
                                availableRelayServers.Add(keys[i], deserializedData);
                        }
                        catch (Exception ex)
                        {
                            // server doesnt exist anymore probably
                            // do more shit here
                            WriteLogMessage("Server " + keys[i] + " does not exist anymore, removing", ConsoleColor.Red);
                            availableRelayServers.Remove(keys[i]);
                        }
                    }
                }

                await Task.Delay(_pingDelay);
            }
        }

        void WriteTitle()
        {
            string t = @"  
  _        _____    __  __                                                
 | |      |  __ \  |  \/  |                                               
 | |      | |__) | | \  / |                                               
 | |      |  _  /  | |\/| |                                               
 | |____  | | \ \  | |  | |                           w  c(..)o   (                  
 |______| |_|  \_\ |_|  |_|                            \__(-)    __)
  _         ____               _____                       /\   (              
 | |       / __ \      /\     |  __ \                     /(_)___)             
 | |      | |  | |    /  \    | |  | |                    w /|                
 | |      | |  | |   / /\ \   | |  | |                     | \                
 | |____  | |__| |  / ____ \  | |__| |                    m  m copyright monkesoft 2021                
 |______|  \____/  /_/    \_\ |_____/                                                                                                                                             
  ____               _                   _   _    _____   ______   _____  
 |  _ \      /\     | |          /\     | \ | |  / ____| |  ____| |  __ \ 
 | |_) |    /  \    | |         /  \    |  \| | | |      | |__    | |__) |
 |  _ <    / /\ \   | |        / /\ \   | . ` | | |      |  __|   |  _  / 
 | |_) |  / ____ \  | |____   / ____ \  | |\  | | |____  | |____  | | \ \ 
 |____/  /_/    \_\ |______| /_/    \_\ |_| \_|  \_____| |______| |_|  \_\
";

            string load = $"Chimp Event Listener Initializing... OK" +
                            "\nHarambe Memorial Initializing...     OK" +
                            "\nBananas Initializing...              OK\n";

            WriteLogMessage(t, ConsoleColor.Green);
            WriteLogMessage(load, ConsoleColor.Cyan);
        }

        static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

    }

    [Serializable]
    public struct RelayStats
    {
        public int ConnectedClients;
        public int RoomCount;
        public int PublicRoomCount;
        public TimeSpan Uptime;
    }

    [Serializable]
    public struct RelayAddress
    {
        public ushort Port;
        public ushort EndpointPort;
        public string Address;
    }

}