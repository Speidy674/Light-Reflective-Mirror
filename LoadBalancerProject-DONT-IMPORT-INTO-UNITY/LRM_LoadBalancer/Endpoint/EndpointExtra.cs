using LightReflectiveMirror.Debug;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LightReflectiveMirror.LoadBalancing
{
    public partial class Endpoint
    {
        static void CacheAllServers()
        {
            foreach (var region in _regionRooms)
            {
                _cachedRegionRooms[region.Key] = JsonConvert.SerializeObject(region.Value,Formatting.Indented);
            }

            foreach (var region in _regionRoomsAppId)
            {
                foreach(var appId in region.Value)
                {
                    _cachedRegionRoomsAppId[region.Key][appId.Key] = JsonConvert.SerializeObject(appId.Value, Formatting.Indented);
                }
            }
        }

        static void ClearAllServersLists()
        {
            foreach (var region in _regionRooms)
                region.Value.Clear();

            foreach (var regionAppId in _regionRoomsAppId)
                regionAppId.Value.Clear();

            foreach (var regionAppId in _cachedRegionRoomsAppId)
                regionAppId.Value.Clear();
        }
    }

    public partial class EndpointServer
    {
        public static List<string> GetLocalIps()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            List<string> bindableIPv4Addresses = new();

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    bindableIPv4Addresses.Add(ip.ToString());
                }
            }

            bool hasLocal = false;

            for (int i = 0; i < bindableIPv4Addresses.Count; i++)
            {
                if (bindableIPv4Addresses[i] == "127.0.0.1")
                    hasLocal = true;
            }

            if (!hasLocal)
                bindableIPv4Addresses.Add("127.0.0.1");

            return bindableIPv4Addresses;
        }
    }
}
