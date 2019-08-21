
namespace Raft.Messages
{

    public class RequestVoteResult
    {
        public string VoterId { get; set; }
        public bool VoteGranted { get; set; }
        public int Term { get; set; }
    }
}