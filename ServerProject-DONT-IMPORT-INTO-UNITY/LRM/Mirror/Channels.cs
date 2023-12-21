using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Mirror
{
	public static class Channels
    {
        public const int Reliable = 0;   // ordered
        public const int Unreliable = 1; // unordered
    }
}