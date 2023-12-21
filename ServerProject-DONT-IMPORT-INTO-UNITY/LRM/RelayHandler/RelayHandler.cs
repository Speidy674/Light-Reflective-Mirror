﻿using Mirror;
using System;
using System.Buffers;
using System.Linq;
using System.Threading.Tasks;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        // constructor for new relay handler
        public RelayHandler(int maxPacketSize)
        {
            _maxPacketSize = maxPacketSize;
            _sendBuffers = ArrayPool<byte>.Create(maxPacketSize, 50);
        }

        /// <summary>
        /// Checks if a server id already is in use.
        /// </summary>
        /// <param name="id">The ID to check for</param>
        /// <returns></returns>
        private bool DoesServerIdExist(string id) => _cachedRooms.ContainsKey(id);

        private string GenerateRoomID()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var randomID = "";
            var random = _cachedRandom;

            do
            {
                randomID = new string(Enumerable.Repeat(chars, Program.conf.RandomlyGeneratedIDLength)
                                                        .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (DoesServerIdExist(randomID));

            return randomID;
        }

        /// <summary>
        /// Generates a random server ID.
        /// </summary>
        /// <returns></returns>
        private string GetRandomServerID()
        {
            if (!Program.conf.UseLoadBalancer)
            {
                return GenerateRoomID();
            }
            else
            {
                // ping load balancer here
                var uri = new Uri($"http://{Program.conf.LoadBalancerAddress}:{Program.conf.LoadBalancerPort}/api/get/id");

                var task = Task.Run(() => Program.httpClient.GetStringAsync(uri));
                task.Wait();

                string randomID = task.Result.Replace("\\r", "").Replace("\\n", "").Trim();

                return randomID;
            }
        }

        /// <summary>
        /// This is called when a client wants to send data to another player.
        /// </summary>
        /// <param name="clientId">The ID of the client who is sending the data</param>
        /// <param name="clientData">The binary data the client is sending</param>
        /// <param name="channel">The channel the client is sending this data on</param>
        /// <param name="sendTo">Who to relay the data to</param>
        private void ProcessData(int clientId, byte[] clientData, int channel, int sendTo = -1)
        {
            if(_cachedClientRooms.TryGetValue(clientId, out Room room) == false)
                return;

            if (room.hostId == clientId)
            {
                if (room.clients.Contains(sendTo))
                {
                    SendData(clientData, channel, sendTo, clientId);
                }
            }
            else
            {
                SendDataToRoomHost(clientId, clientData, channel, room);
            }
        }

        private void SendData(byte[] clientData, int channel, int sendTo, int senderId)
        {
            if (clientData.Length > _maxPacketSize)
            {
                Program.transport.ServerDisconnect(senderId);
                Program.WriteLogMessage($"Client {senderId} tried to send more than max packet size! Disconnecting...");
                return;
            }

            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(_maxPacketSize);
   
            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
            sendBuffer.WriteBytes(ref pos, clientData);

            Program.transport.ServerSend(sendTo, new ArraySegment<byte>(sendBuffer, 0, pos), channel);
            _sendBuffers.Return(sendBuffer);
        }

        private void SendDataToRoomHost(int senderId, byte[] clientData, int channel, Room room)
        {
            if(clientData.Length > _maxPacketSize)
            {
                Program.transport.ServerDisconnect(senderId);
                Program.WriteLogMessage($"Client {senderId} tried to send more than max packet size! Disconnecting...");
                return;
            }
            
            // We are not the host, so send the data to the host.
            int pos = 0; 
            byte[] sendBuffer = _sendBuffers.Rent(_maxPacketSize);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetData);
            sendBuffer.WriteBytes(ref pos, clientData);
            sendBuffer.WriteInt(ref pos, senderId);

            Program.transport.ServerSend(room.hostId, new ArraySegment<byte>(sendBuffer, 0, pos), channel);
            _sendBuffers.Return(sendBuffer);
        }

        /// <summary>
        /// Called when a client wants to request their own ID.
        /// </summary>
        /// <param name="clientId">The client requesting their ID</param>
        private void SendClientID(int clientId)
        {
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.GetID);
            sendBuffer.WriteInt(ref pos, clientId);

            Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
            _sendBuffers.Return(sendBuffer);
        }
    }
}
