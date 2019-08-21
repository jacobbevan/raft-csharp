using System.Collections.Generic;
using Raft.Messages;

namespace Raft.Server
{
    public interface IServer
    {
        string Id{get;}

        RaftServer.RaftServerState State {get;}

        AppendEntriesResult AppendEntries(AppendEntriesCommand request);
        RequestVoteResult RequestVote(RequestVoteCommand request);
        void Initialise(IEnumerable<IServerProxy> servers);
    }
}