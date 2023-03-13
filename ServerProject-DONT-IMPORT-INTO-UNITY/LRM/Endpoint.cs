using Grapevine;
using LightReflectiveMirror.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LightReflectiveMirror.Endpoints
{
    [Serializable]
    struct RelayStats
    {
        public int ConnectedClients;
        public int RoomCount;
        public int PublicRoomCount;
        public TimeSpan Uptime;
    }

    [RestResource(BasePath = "/api/")]
    public class Endpoint
    {

        private static Dictionary<int,string> _cachedServerListAppId = new ();
        private static Dictionary<int, string> _cachedCompressedServerListAppId = new ();


        private static string _cachedServerList = "[]";
        private static string _cachedCompressedServerList;
        public static DateTime lastPing = DateTime.Now;

        private static List<Room> _rooms { get => Program.instance.GetRooms().Where(x => x.isPublic).ToList(); }
        private static List<List<Room>> _appRooms { get => Program.instance.GetRooms().GroupBy(x => x.appId).Select(grp => grp.ToList()).ToList(); }

        private RelayStats _stats
        {
            get => new()
            {
                ConnectedClients = Program.instance.GetConnections(),
                RoomCount = Program.instance.GetRooms().Count,
                PublicRoomCount = Program.instance.GetPublicRoomCount(),
                Uptime = Program.instance.GetUptime()
            };
        }

        public static void RoomsModified()
        {
            _cachedServerList = JsonConvert.SerializeObject(_rooms, Formatting.Indented);
            _cachedCompressedServerList = _cachedServerList.Compress();

            _cachedServerListAppId.Clear();
            _cachedCompressedServerListAppId.Clear();

            _appRooms.ForEach((rooms) =>
            {
                string jsonRooms = JsonConvert.SerializeObject(rooms, Formatting.Indented);
                _cachedServerListAppId.Add(rooms.First().appId, jsonRooms);
                _cachedCompressedServerListAppId.Add(rooms.First().appId, jsonRooms.Compress());
            });


            if (Program.conf.UseLoadBalancer)
                Program.instance.UpdateLoadBalancerServers();
        }

        [RestRoute("Get", "/stats")]
        public async Task Stats(IHttpContext context)
        {
            lastPing = DateTime.Now;
            string json = JsonConvert.SerializeObject(_stats, Formatting.Indented);
            await context.Response.SendResponseAsync(json);
        }

        [RestRoute("Get", "/servers")]
        public async Task ServerList(IHttpContext context)
        {
            if (Program.conf.EndpointServerList)
            {
                await context.Response.SendResponseAsync(_cachedServerList);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Get", "/servers/{appId:num}")]
        public async Task ServerListAppId(IHttpContext context)
        {
            if (Program.conf.EndpointServerList)
            {
                int appId = int.Parse(context.Request.PathParameters["appId"]);
                await context.Response.SendResponseAsync(_cachedServerListAppId.GetValueOrDefault(appId, "[]"));
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Get", "/compressed/servers")]
        public async Task ServerListCompressed(IHttpContext context)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");

            if (Program.conf.EndpointServerList)
            {
                await context.Response.SendResponseAsync(_cachedCompressedServerList);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Get", "/compressed/servers/{appId:num}")]
        public async Task ServerListCompressedAppId(IHttpContext context)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");

            if (Program.conf.EndpointServerList)
            {
                int appId = int.Parse(context.Request.PathParameters["appId"]);
                await context.Response.SendResponseAsync(_cachedCompressedServerListAppId.GetValueOrDefault(appId,"[]".Compress()));
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        [RestRoute("Options", "/compressed/servers")]
        public async Task ServerListCompressedOptions(IHttpContext context)
        {
            var originHeaders = context.Request.Headers["Access-Control-Request-Headers"];

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", originHeaders);

            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
        }
    }

    public class EndpointServer
    {
        public bool Start(ushort port = 8080, bool ssl = false)
        {
            try
            {
                var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

                var server = new RestServerBuilder(new ServiceCollection(), config,
                (services) =>
                {
                    services.AddLogging(configure => configure.AddConsole());
                    services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.None);
                }, (server) =>
                {
                    if (ssl)
                    {
                        server.Prefixes.Add($"https://*:{port}/");
                    }
                    else
                    {
                        server.Prefixes.Add($"http://*:{port}/");
                    }
                }).Build();

                server.Router.Options.SendExceptionMessages = false;
                server.Start();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

        }
    }
}

