using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raft.Messages;
using Raft.Server;

namespace Raft.Proxies
{

    public class ReliableProxy : IServerProxy
    {
        private IServer _server;
        private int _latency;

        public ReliableProxy(IServer server, int latency)
        {
            _server = server;
            _latency = latency;
        }
        public string Id => _server.Id;
        public Task<AppendEntriesResult> AppendEntries(AppendEntriesCommand request)
        {
            return Utility.AddLatency(_server.AppendEntries, request, _latency);
        }

        public Task<RequestVoteResult> RequestVote(RequestVoteCommand request)
        {
            return Utility.AddLatency(_server.RequestVote, request, _latency);
        }
    }
}