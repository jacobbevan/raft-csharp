using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raft.Messages;
using Raft.Server;

namespace Raft.Proxies
{

    public class ProxyWithTimeouts : IServerProxy
    {
        private IServerProxy _proxy;
        private double _timeoutPct;
        private Random _rand = new Random();
        public ProxyWithTimeouts(IServerProxy proxy, double timeoutPct)
        {
            _proxy = proxy;
            _timeoutPct = timeoutPct;
        }

        public string Id => _proxy.Id;

        public async Task<AppendEntriesResult> AppendEntries(AppendEntriesCommand request)
        {
            if(_rand.NextDouble() < _timeoutPct)
            {
                await Task.Delay(-1);
            }
            return await _proxy.AppendEntries(request);
        }

        public async Task<RequestVoteResult> RequestVote(RequestVoteCommand request)
        {
            if(_rand.NextDouble() < _timeoutPct)
            {
                await Task.Delay(-1);
            }
            return await _proxy.RequestVote(request);
        }
    }
}