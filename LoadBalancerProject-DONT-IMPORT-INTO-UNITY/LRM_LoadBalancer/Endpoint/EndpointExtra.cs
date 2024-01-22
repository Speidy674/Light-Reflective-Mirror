using LightReflectiveMirror.Debug;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {

        static bool updateServerListRunning = false;
        static bool updateServerListQueue = false;


        static void startupdateServerList()
        {
            if (updateServerListRunning)
            {
                updateServerListQueue = true;
                return;
            }
            else
            {
                updateServerListRunning = true;
                if (updateServerListQueue) updateServerListQueue = false;

                updateServerList();
            }

        }

        static async void updateServerList()
        {
            Logger.WriteLogMessage($"Start Update Server List", ConsoleColor.Cyan);
            var relays = Program.instance.availableRelayServers.ToList();
            ClearAllServersLists();
            List<Room> requestedRooms;

            Program.cachedRooms.Clear();

            for (int i = 0; i < relays.Count; i++)
            {
                try
                {
                    requestedRooms = await Program.instance.RequestServerListFromNode(relays[i].Key.address, relays[i].Key.endpointPort);
                    _regionRooms[LRMRegions.Any].AddRange(requestedRooms);
                    _regionRooms[relays[i].Key.serverRegion].AddRange(requestedRooms);

                    List<List<Room>> _appRooms = requestedRooms.GroupBy(x => x.appId).Select(grp => grp.ToList()).ToList();

                    _appRooms.ForEach((rooms) =>
                    {
                        string jsonRooms = JsonConvert.SerializeObject(rooms, Formatting.Indented);
                        _regionRoomsAppId[LRMRegions.Any].Add(rooms.First().appId, rooms);
                    });

                    for (int x = 0; x < requestedRooms.Count; x++)
                        if (!Program.cachedRooms.TryAdd(requestedRooms[x].serverId, requestedRooms[x]))
                            Logger.ForceLogMessage("Conflicting Rooms! (That's ok)", ConsoleColor.Yellow);
                }

                catch (Exception e)
                {
                    Logger.ForceLogMessage("Error UpdateServerList: " + e.Message, ConsoleColor.Red);
                    updateServerListQueue = true;
                    return;
                }
            }
            Logger.WriteLogMessage($"Try Calling CacheAllServers", ConsoleColor.Cyan);
            CacheAllServers();
            Logger.WriteLogMessage($"Finished Update Server List", ConsoleColor.Cyan);

            updateServerListRunning = false;
            if (updateServerListQueue) startupdateServerList();
        }

        static void CacheAllServers()
        {
            foreach (var region in _regionRooms)
            {
                _cachedRegionRooms[region.Key] = JsonConvert.SerializeObject(region.Value, Formatting.Indented);
            }

            foreach (var region in _regionRoomsAppId)
            {
                foreach (var appId in region.Value)
                {
                    _cachedRegionRoomsAppId[region.Key][appId.Key] = JsonConvert.SerializeObject(appId.Value, Formatting.Indented);
                }
            }
        }

        static void ClearAllServersLists()
        {
            foreach (var region in _regionRooms)
                region.Value.Clear();

            foreach (var region in _cachedRegionRooms)
                _cachedRegionRooms[region.Key] = "[]";

            foreach (var regionAppId in _regionRoomsAppId)
                regionAppId.Value.Clear();

            foreach (var regionAppId in _cachedRegionRoomsAppId)
                regionAppId.Value.Clear();
        }
    }

    public partial class EndpointServer
    {

    }
}
