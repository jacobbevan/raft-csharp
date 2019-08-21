using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raft.Messages;
using Raft.Server;

namespace Raft.Proxies
{
    public static class Utility
    {
        public static async Task<V> AddLatency<U,V>(Func<U,V> func, U arg, int latency)
        {
            await Task.Delay(latency);
            var result = func(arg);
            await Task.Delay(latency);
            return result;
        }
    }
}