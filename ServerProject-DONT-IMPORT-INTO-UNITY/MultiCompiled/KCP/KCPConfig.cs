using Grapevine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kcp2k
{
    class KCPConfig
    {
        public bool DualMode = true;
        public bool NoDelay = true;
        public uint Interval = 10;
        public int Timeout = 10000;
        public int RecvBufferSize = 1024 * 1027 * 7;
        public int SendBufferSize = 1024 * 1027 * 7;

        public int FastResend = 2;
        public bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        public uint ReceiveWindowSize = 4096; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.
        public uint SendWindowSize = 4096; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.
        public uint MaxRetransmit = Kcp.DEADLINK * 2; // default prematurely disconnects a lot of people (#3022). use 2x.
        public bool MaximizeSocketBuffers = true;
    }
}
