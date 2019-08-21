using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raft.Messages;
using Raft.Server;

namespace Raft.Proxies
{

    public class ProxyWithExceptions : IServerProxy
    {
        private IServerProxy _proxy;
        private double _exceptionPct;
        private Random _rand = new Random();
        public ProxyWithExceptions(IServerProxy proxy, double exceptionPct)
        {
            _proxy = proxy;
            _exceptionPct = exceptionPct;
        }

        public string Id => _proxy.Id;

        public async Task<AppendEntriesResult> AppendEntries(AppendEntriesCommand request)
        {
            if(_rand.NextDouble() < _exceptionPct)
            {
                throw new Exception("Network related exception");
            }
            return await _proxy.AppendEntries(request);
        }

        public async Task<RequestVoteResult> RequestVote(RequestVoteCommand request)
        {
            if(_rand.NextDouble() < _exceptionPct)
            {
                throw new Exception("Network related exception");
            }
            return await _proxy.RequestVote(request);
        }
    }
}