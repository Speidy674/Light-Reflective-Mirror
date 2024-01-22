using Grapevine;
using LightReflectiveMirror.Debug;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using HttpStatusCode = Grapevine.HttpStatusCode;

namespace LightReflectiveMirror.LoadBalancing
{

    [RestResource(BasePath = "/api/")]
    public partial class Endpoint
    {
        /// <summary>
        /// Sent from an LRM server node
        /// adds it to the list if authenticated.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/auth")]
        public async Task ReceiveAuthKey(IHttpContext context)
        {
            var req = context.Request;
            string receivedAuthKey = req.Headers["Authorization"];
            string endpointPort = req.Headers["x-EndpointPort"];
            string gamePort = req.Headers["x-GamePort"];
            string publicIP = req.Headers["x-PIP"];
            string region = req.Headers["x-Region"];

            string address = context.Request.RemoteEndPoint.Address.ToString();
            Logger.WriteLogMessage("Received auth req [" + receivedAuthKey + "] == [" + Program.conf.AuthKey + "]", ConsoleColor.Cyan);

            // if server is authenticated
            if (receivedAuthKey != null && region != null && int.TryParse(region, out int regionId) &&
                address != null && endpointPort != null && gamePort != null && receivedAuthKey == Program.conf.AuthKey)
            {
                Logger.WriteLogMessage($"Server accepted: {address}:{gamePort}");

                try
                {
                    var _gamePort = Convert.ToUInt16(gamePort);
                    var _endpointPort = Convert.ToUInt16(endpointPort);
                    await Program.instance.AddServer(address, _gamePort, _endpointPort, publicIP, regionId);
                    startupdateServerList();
                }
                catch
                {
                    await context.Response.SendResponseAsync(HttpStatusCode.BadRequest);
                }

                await context.Response.SendResponseAsync(HttpStatusCode.Ok);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Called on the load balancer when a relay node had a change in their servers. This recompiles the cached values.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/roomsupdated")]
        public async Task ServerListUpdate(IHttpContext context)
        {
            // Dont allow unauthorizated access waste computing resources.
            string auth = context.Request.Headers["Authorization"];

            if (!string.IsNullOrEmpty(auth) && auth == Program.conf.AuthKey)
            {
                startupdateServerList();
                await context.Response.SendResponseAsync(HttpStatusCode.Ok);
            }
            else
                await context.Response.SendResponseAsync(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Hooks into from unity side, client will call this to 
        /// find the least populated server to join
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/join/")]
        public async Task JoinRelay(IHttpContext context)
        {
            // need to copy over in order to avoid
            // collection being modified while iterating.
            var servers = Program.instance.availableRelayServers.ToList();
            var low = lowest;
            var lowestTotal = lowest;
            var region = context.Request.Headers["x-Region"];

            // If we cant parse it (due to not being included or not being valid) then just join any region.
            int regionID = int.TryParse(region, out regionID) ? regionID : 0;

            if (servers.Count == 0)
            {
                await context.Response.SendResponseAsync(HttpStatusCode.RangeNotSatisfiable);
                return;
            }

            bool foundServerInRegion = false;
            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].Value.connectedClients < low.Value.connectedClients)
                {
                    if ((int)servers[i].Key.serverRegion == regionID)
                    {
                        low = servers[i];
                        foundServerInRegion = true;
                    }

                    lowestTotal = servers[i];
                }
            }

            // If the region didnt have a single node, then just give him the lowest node we looped over.
            if (!foundServerInRegion)
                low = lowestTotal;

            // respond with the server ip
            // if the string is still dummy then theres no servers
            await context.Response.SendResponseAsync(low.Key.address != "Dummy" ? JsonConvert.SerializeObject(low.Key) : HttpStatusCode.InternalServerError);
        }

        [RestRoute("Options", "/join/")]
        public async Task JoinRelayOptions(IHttpContext context)
        {
            var originHeaders = context.Request.Headers["Access-Control-Request-Headers"];

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", originHeaders);

            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
        }

        /// <summary>
        /// Returns all the servers on all the relay nodes.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/masterlist/")]
        public async Task GetMasterServerList(IHttpContext context)
        {
            string region = context.Request.Headers["x-Region"];


            if (int.TryParse("Info:" + region, out int regionID))
            {
                await context.Response.SendResponseAsync(_cachedRegionRooms[(LRMRegions)regionID]);
                return;
            }

            // They didnt submit a region header, just give them all servers as they probably are viewing in browser.
            await context.Response.SendResponseAsync(_cachedRegionRooms[LRMRegions.Any]);
        }

        /// <summary>
        /// Returns all the servers from AppId on all the relay nodes.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/masterlist/{appId:num}")]
        public async Task GetMasterServerListApp(IHttpContext context)
        {
            string region = context.Request.Headers["x-Region"];
            int appId = int.Parse(context.Request.PathParameters["appId"]);


            if (int.TryParse("Info:" + region, out int regionID))
            {
                await context.Response.SendResponseAsync(_cachedRegionRoomsAppId[(LRMRegions)regionID].GetValueOrDefault(appId,"[]"));
                return;
            }

            // They didnt submit a region header, just give them all servers as they probably are viewing in browser.
            await context.Response.SendResponseAsync(_cachedRegionRoomsAppId[LRMRegions.Any].GetValueOrDefault(appId, "[]"));
        }

        [RestRoute("Options", "/masterlist/")]
        public async Task GetMasterServerListOptions(IHttpContext context)
        {
            var originHeaders = context.Request.Headers["Access-Control-Request-Headers"];

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", originHeaders);

            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
        }

        /// <summary>
        /// Returns stats. you're welcome
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Get", "/stats/")]
        public async Task GetStats(IHttpContext context)
        {
            await context.Response.SendResponseAsync(JsonConvert.SerializeObject(_stats));
        }

        [RestRoute("Get", "/get/id")]
        public async Task GetServerID(IHttpContext context)
        {
            await context.Response.SendResponseAsync(Program.instance.GenerateServerID());
        }

        public static void Initialize()
        {
            foreach (LRMRegions region in Enum.GetValues(typeof(LRMRegions)))
            {
                _regionRooms.Add(region, new());
                _cachedRegionRooms.Add(region, "[]");

                _regionRoomsAppId.Add(region, new());
                _cachedRegionRoomsAppId.Add(region, new());
            }
        }
    }

    #region Startup

    public partial class EndpointServer
    {
        public bool Start(ushort port = 7070, bool ssl = false)
        {
            try
            {
                // Initialize the region variables
                Endpoint.Initialize();

                var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
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
                        server.Prefixes.Add($"https://+:{port}/");
                    }
                    else
                    {
                        server.Prefixes.Add($"http://+:{port}/");
                    }

                }).Build();

                server.Router.Options.SendExceptionMessages = true;
                server.Start();

                return true;
            }
            catch (Exception e)
            {
                Logger.ForceLogMessage(e.ToString(), ConsoleColor.Red);
                return false;
            }
        }


        #endregion

    }

}