namespace Raft.Messages
{
    public class RequestVoteCommand
    {
        public string CandidateId { get; set; }
        public int CurrentTerm { get; set; }
    }
}