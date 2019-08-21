using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raft.Messages;
using Raft.Server;

namespace Raft.Proxies
{

    

    public class ProxyWithPartitions : IServerProxy
    {
        public class PartitionConfiguration
        {
            private bool _isPartitioned = false;
            public Dictionary<string,int> _partitions;
            public PartitionConfiguration(Dictionary<string, int> partitions, bool isPartitioned)
            {
                _isPartitioned = isPartitioned;
                _partitions = partitions;
            }

            public void Partition()
            {
                _isPartitioned = true;
            }

            public void Heal()
            {
                _isPartitioned = false;
            }

            public bool Reachable(string id1, string id2)
            {
                return !_isPartitioned  ||  (_partitions[id1] == _partitions[id2]);
            }
        }

        private IServerProxy _proxy;
        private PartitionConfiguration _config;
        public string Id => _proxy.Id;
        
        public ProxyWithPartitions(IServerProxy proxy, PartitionConfiguration config)
        {
            _proxy = proxy;
            _config = config;
        }
        public async Task<AppendEntriesResult> AppendEntries(AppendEntriesCommand request)
        {            
            if(_config.Reachable(request.LeaderId,_proxy.Id)) 
            {
                return await _proxy.AppendEntries(request);
            }
            throw new Exception("Network related exception");
        }

        public async Task<RequestVoteResult> RequestVote(RequestVoteCommand request)
        {
            //TODO base interface for all requests including SenderId
            //TODO Factory for proxies, most can be constructed in an AOP type fashion
            if(_config.Reachable(request.CandidateId,_proxy.Id)) 
            {
                return await _proxy.RequestVote(request);
            }
            throw new Exception("Network related exception");
        }
    }
}