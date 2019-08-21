using System;
using static Raft.Server.RaftServer;

namespace Raft.Audit
{
    public sealed class AuditRecord
    {
        public enum AuditRecordType
        {
            BecomeCandidate,
            BecomeFollower,
            BecomeLeader,
            SendHeartbeat,
            RecAppendEntries,
            StartElection,
            RecVote,
            DetectStaleTerm,
            AcceptVoteRequest,
            RejectVoteRequest,
            RecHeartbeatResponse,
            RejectAppendEntries,
            RecVoteRequest
        }

        public string Candidate { get; set; }
        public int Term { get; set; }
        public DateTime When { get; set; }        
        public AuditRecordType Type { get; set; }
        public string ExtraInfo { get; set; }
        public RaftServerState State { get; set; }
        
        public AuditRecord(AuditRecordType type, string candidate, RaftServerState state, int term, string extraInfo = "")
        {
            When = DateTime.Now;

            Type = type;
            Candidate = candidate;
            Term = term;
            ExtraInfo = extraInfo;
            State = state;

            Console.WriteLine($"Time: {When:HH:mm:ss:FFF} Action: {Type} Id: {Candidate} State: {State} Term: {Term} Info: {extraInfo}");
        }

    }
}