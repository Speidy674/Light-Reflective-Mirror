using System;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace Mirror.SimpleWeb
{
    public static class Log
    {
        public enum Levels
        {
            none = 0,
            error = 1,
            warn = 2,
            info = 3,
            verbose = 4,
        }

        public static Levels level = Levels.none;

        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null)
        {
            return BitConverter.ToString(buffer, offset, length ?? buffer.Length);
        }

        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            if (level < Levels.verbose)
                return;

            Console.WriteLine($"[SimpleWebTransport] VERBOSE: <color=cyan>{label}: {BufferToString(buffer, offset, length)}</color>");
        }

        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            if (level < Levels.verbose)
                return;

            Console.WriteLine($"[SimpleWebTransport] VERBOSE: <color=cyan>{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}</color>");
        }

        public static void Verbose(string msg, bool showColor = true)
        {
            if (level < Levels.verbose)
                return;

            if (showColor)
                Console.WriteLine($"[SimpleWebTransport] VERBOSE: <color=cyan>{msg}</color>");
            else
                Console.WriteLine($"[SimpleWebTransport] VERBOSE: {msg}");
        }

        public static void Info(string msg, bool showColor = true)
        {
            if (level < Levels.info)
                return;

            if (showColor)
                Console.WriteLine($"[SimpleWebTransport] INFO: <color=cyan>{msg}</color>");
            else
                Console.WriteLine($"[SimpleWebTransport] INFO: {msg}");
        }

        public static void InfoException(Exception e)
        {
            if (level < Levels.info)
                return;

            Console.WriteLine($"[SimpleWebTransport] INFO_EXCEPTION: <color=cyan>{e.GetType().Name}</color> Message: {e.Message}\n{e.StackTrace}\n\n");
        }

        public static void Warn(string msg, bool showColor = true)
        {
            if (level < Levels.warn)
                return;

            if (showColor)
                Console.WriteLine($"[SimpleWebTransport] WARN: <color=orange>{msg}</color>");
            else
                Console.WriteLine($"[SimpleWebTransport] WARN: {msg}");
        }

        public static void Error(string msg, bool showColor = true)
        {
            if (level < Levels.error)
                return;

            if (showColor)
                Console.Error.WriteLine($"[SimpleWebTransport] ERROR: <color=red>{msg}</color>");
            else
                Console.Error.WriteLine($"[SimpleWebTransport] ERROR: {msg}");
        }

        public static void Exception(Exception e)
        {
            Console.Error.WriteLine($"[SimpleWebTransport] EXCEPTION: <color=red>{e.GetType().Name}</color> Message: {e.Message}\n{e.StackTrace}\n\n");
        }
    }
}
