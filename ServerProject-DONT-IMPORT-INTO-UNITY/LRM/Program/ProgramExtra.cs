using System;
using System.Collections.Generic;
using System.Linq;

namespace LightReflectiveMirror
{
    partial class Program
    {
        public List<Room> GetRooms() => _relay.rooms;
        public int GetConnections() => _currentConnections.Count;
        public TimeSpan GetUptime() => DateTime.Now - _startupTime;
        public int GetPublicRoomCount() => _relay.rooms.Where(x => x.isPublic).Count();
        
        /// <summary>
        /// Assigns unique id for client id
        /// </summary>
        /// <param name="uniqueId">Unique id that is assigned to a client</param>
        /// <param name="clientId">Client id that was generated for this connection</param>
        public void AssignUniqueId(string uniqueId, int clientId)
        {
            _uniqueIdToClientId.Add(uniqueId, clientId);
        }
        
        /// <summary>
        /// Tries to remove unique id from the dictionary
        /// </summary>
        /// <param name="clientId"> Client id that was generated for this connection </param>
        public void TryRemoveUniqueId(int clientId)
        {
            if (_uniqueIdToClientId.TryGetBySecond(clientId, out string uniqueId))
                _uniqueIdToClientId.Remove(uniqueId);
        }
        
        /// <summary>
        /// Checks if unique id is assigned to a client
        /// </summary>
        /// <param name="uniqueId"> Unique id that is assigned to a client </param>
        /// <returns> True if unique id is assigned to a client, false if not </returns>
        public bool IsClientConnected(string uniqueId)
        {
            return _uniqueIdToClientId.TryGetByFirst(uniqueId, out _);
        }

        public static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        private static void GetPublicIP()
        {
            try
            {
                // easier to just ping an outside source to get our public ip
                publicIP = webClient.DownloadString("https://api.ipify.org/").Replace("\\r", "").Replace("\\n", "").Trim();
            }
            catch
            {
                WriteLogMessage("Failed to reach public IP endpoint! Using loopback address.", ConsoleColor.Yellow);
                publicIP = "127.0.0.1";
            }
        }

        void WriteTitle()
        {
            string t = @"  
                           w  c(..)o   (
  _       _____   __  __    \__(-)    __)
 | |     |  __ \ |  \/  |       /\   (
 | |     | |__) || \  / |      /(_)___)
 | |     |  _  / | |\/| |      w /|
 | |____ | | \ \ | |  | |       | \
 |______||_|  \_\|_|  |_|      m  m copyright monkesoft 2021

";

            string load = $"Chimp Event Listener Initializing... OK" +
                            "\nHarambe Memorial Initializing...     OK" +
                            "\nBananas Initializing...              OK\n";

            WriteLogMessage(t, ConsoleColor.Green);
            WriteLogMessage(load, ConsoleColor.Cyan);
        }
    }
}
