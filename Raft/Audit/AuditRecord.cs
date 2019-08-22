using System;
using Serilog;
using Serilog.Events;
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
        public string Id { get; set; }
        public RaftServerState State { get; set; }
        public AuditRecordType Type { get; set; }
        public int Term { get; set; }
        public string ExtraInfo { get; set; }
        public DateTime When { get; set; }        
        
        public AuditRecord(AuditRecordType type, string candidate, RaftServerState state, int term, string extraInfo = "")
        {
            When = DateTime.Now;

            Type = type;
            Id = candidate;
            Term = term;
            ExtraInfo = extraInfo;
            State = state;

            Log.Write(GetLogEventLevel(type), "Raft audit: {@item}", this);
        }

        private LogEventLevel GetLogEventLevel(AuditRecordType recordType)
        {
            
            switch(recordType)
            {
                case AuditRecordType.DetectStaleTerm:
                    return LogEventLevel.Warning;
                case AuditRecordType.BecomeCandidate:
                case AuditRecordType.BecomeFollower:
                case AuditRecordType.BecomeLeader:
                case AuditRecordType.RecVote:
                case AuditRecordType.RecVoteRequest:
                case AuditRecordType.RejectAppendEntries:
                case AuditRecordType.RejectVoteRequest:
                case AuditRecordType.AcceptVoteRequest:
                    return LogEventLevel.Information;
                case AuditRecordType.SendHeartbeat:
                case AuditRecordType.RecAppendEntries:
                case AuditRecordType.RecHeartbeatResponse:
                    return LogEventLevel.Debug;
                default:                    
                    return LogEventLevel.Error;
            }
        }
    }
}