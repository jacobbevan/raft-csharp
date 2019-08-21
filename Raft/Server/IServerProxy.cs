using System.Threading.Tasks;
using Raft.Messages;

namespace Raft.Server
{
    public interface IServerProxy
    {
        string Id { get; }

        Task<AppendEntriesResult> AppendEntries(AppendEntriesCommand request);
        Task<RequestVoteResult> RequestVote(RequestVoteCommand request);    
    }
}