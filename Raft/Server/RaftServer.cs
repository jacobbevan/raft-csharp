using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raft.Audit;
using Raft.Messages;
using Serilog;
using static Raft.Audit.AuditRecord;

namespace Raft.Server
{
    public class RaftServer : IServer, IDisposable
    {


        public enum RaftServerState
        {
            Leader,
            Follower,
            Candidate
        }



        private object _lock = new object();
        private volatile RaftServerState _state = RaftServerState.Follower;

        private volatile int _currentTerm;
        private string _votedFor;
        private CommandLog _log;
        private IAuditLog _auditLog;
        private IPlanner _planner;
        private int _votesReceived = 0;
        private IList<IServerProxy> _servers;
        private CancellationTokenSource _taskCancellation = new CancellationTokenSource(); 
        public string Id { get; private set; }
        public RaftServerState State {get {return _state;}}
        public int  CurrentTerm {get {return _currentTerm;}}
        public RaftServer(string id, IAuditLog auditLog, IPlanner planner)
        {
            Id = id;
            _auditLog = auditLog;
            _planner = planner;

        }

        public void Initialise(IEnumerable<IServerProxy> servers)
        {
            _servers = new List<IServerProxy>(servers);
            BecomeFollower(0);
        }

        public AppendEntriesResult AppendEntries(AppendEntriesCommand request)
        {
            lock(_lock)
            {
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.RecAppendEntries, Id, _state, _currentTerm));

                //this instance considers sender's Term to be stale - reject request and complete.

                //NOT IMPLEMENTED return false if log doesn't contain an entry. 
                //NOT IMPLEMENTED if conflict, delete existing entry

                if(_currentTerm > request.Term) {
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.RejectAppendEntries, Id, _state,_currentTerm));
                    return new AppendEntriesResult {
                        Term = _currentTerm,
                        Success = false
                    };
                }

                //we should become a follower, irrespective of current state
                BecomeFollower(request.Term);

                return new AppendEntriesResult {
                    Term = _currentTerm,
                    Success = true
                };
            }
        }

        public RequestVoteResult RequestVote(RequestVoteCommand request)
        {   
            lock(_lock)
            {
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.RecVoteRequest, Id, _state,_currentTerm, $"Requestor : {request.CandidateId}"));

                BecomeFollowerIfTermIsStale(request.CurrentTerm);

                if((_votedFor == request.CandidateId || _votedFor == null) && request.CurrentTerm >= _currentTerm) 
                {
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.AcceptVoteRequest, Id, _state, _currentTerm));
                    _votedFor = request.CandidateId;
                    return new RequestVoteResult 
                    {
                        Term  = _currentTerm,
                        VoteGranted = true,
                        VoterId = Id
                    };
                }
                else
                {
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.RejectVoteRequest, Id, _state,_currentTerm));
                    return new RequestVoteResult
                    {
                        Term = _currentTerm,
                        VoteGranted = false
                    };
                }
            }
        }

        private void ReceiveVote(RequestVoteResult voteResult)
        {
            lock(_lock)
            {
                
                if(!BecomeFollowerIfTermIsStale(voteResult.Term))
                {
                    if(_state == RaftServerState.Candidate && voteResult.VoteGranted && _currentTerm == voteResult.Term)
                    {
                        _auditLog.LogRecord(new AuditRecord(AuditRecordType.RecVote, Id, _state, _currentTerm, $"Voter: {voteResult.VoterId}"));
                        _votesReceived++;
                        if(_votesReceived > (_servers.Count + 1)  / 2)
                        {
                            BecomeLeader();
                        }
                    }
                }
            }
        }

        private bool StaleTermCheck(int termToCompare)
        {
            lock(_lock)
            {
                if(termToCompare > _currentTerm)
                {
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.DetectStaleTerm, Id, _state, _currentTerm));
                    return true;
                }
                return false;
            }
        }

        private bool BecomeFollowerIfTermIsStale(int termToCompare)
        {
            if(StaleTermCheck(termToCompare))
            {
                BecomeFollower(termToCompare);
                return true;
            }
            return false;
        }

        private void BecomeLeader()
        {
            lock(_lock)
            {
                CancelScheduledEvents();
                _state = RaftServerState.Leader;
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeLeader, Id, _state, _currentTerm));
                Task.Run(SendHeartbeat);
            }
        }

        private async Task SendHeartbeat()
        {
            List<IServerProxy> servers;
            AppendEntriesCommand heartBeatCmd;
            lock(_lock)
            {
                servers = new List<IServerProxy>(_servers);
                heartBeatCmd = new AppendEntriesCommand
                {
                    Term = _currentTerm,
                    LeaderId = Id
                };
            }

            _auditLog.LogRecord(new AuditRecord(AuditRecordType.SendHeartbeat, Id, _state, _currentTerm));

            while(_state == RaftServerState.Leader)
            {
                //TODO error handling, cancellation
                var pendingRequests = new List<Task>(
                    servers.Select(
                        s=>SendAppendEntriesReceiveAck(s, heartBeatCmd)
                    )
                );

                await _planner.HeatbeatDelay();
            }
        }

        private async Task SendAppendEntriesReceiveAck(IServerProxy server, AppendEntriesCommand cmd)
        {

            try
            {
                var response = await server.AppendEntries(cmd);
                BecomeFollowerIfTermIsStale(response.Term);
            }
            catch(Exception)
            {
                //TODO exception logging to separate channel
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.AppendEntriesRPCFailure, Id, _state, _currentTerm, $"RPC to: {server.Id}"));
            }
        }



        private void ResetVotingRecord()
        {
            _votedFor = null;
            _votesReceived = 0;
        }

        private void UpdateTerm(int term)
        {
            if(term < _currentTerm)
            {
                var ex = new Exception($"Requested to become a Follower with term {term} when term is {_currentTerm}");
                Log.Fatal(ex, "Requested to become a Follower with term {@t1} when term is {@t2}",  term, _currentTerm);
                throw ex;
            }
            
            if(term > _currentTerm)
            {
                ResetVotingRecord();
            }
            _currentTerm = term;

        }
        public Task BecomeFollower(int term)
        {
            lock(_lock)
            {
                CancelScheduledEvents();
                UpdateTerm(term);

                //Become follower can be called when we already are one (e.g. on receipt of a heartbeat)
                if(_state != RaftServerState.Follower)
                {
                    _state = RaftServerState.Follower;
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeFollower, Id, _state, _currentTerm));
                }
                return Task.Run(ScheduleFollowerTimeout);
            }
        }

        private void CancelScheduledEvents()
        {
            _taskCancellation.Cancel();
            //do not call dispose: does not seem to be required, causes issues with other threads checking the token after disposal
            _taskCancellation = new CancellationTokenSource();
        }

        private async Task ScheduleFollowerTimeout()
        {
            var token = _taskCancellation.Token;

            await _planner.ElectionDelay();  

            if(token.IsCancellationRequested)
            {
                Log.Debug("Election timeout cancelled for candidate {@}", Id);
            }
            else
            {
                await BecomeCandidate();           
            }
        } 

        private async Task RequestAndReceiveVote(IServerProxy server, CancellationToken token)
        {
            //retry indefinitely on exception (until term expires)
            while(true)
            {
                try
                {
                    if(token.IsCancellationRequested)
                    {
                        return;
                    }

                    var result = await server.RequestVote(
                        new RequestVoteCommand {
                            CandidateId = Id,
                            CurrentTerm = _currentTerm 
                        }
                    );
                    
                    ReceiveVote(result);
                    return;
                }
                catch(Exception)
                {
                    //TODO exception logging to separate channel
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.RecVoteRPCFailure, Id, _state, _currentTerm, $"RPC to: {server.Id}"));
                    await _planner.RetryDelay();
                }                
            }
        }

        public async Task BecomeCandidate()
        {            
            _state = RaftServerState.Candidate;
            _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeCandidate, Id, _state, _currentTerm));

            while(_state == RaftServerState.Candidate)
            {
                lock(_lock)
                {
                    CancelScheduledEvents();
                    _currentTerm++;
                    _votesReceived = 0;
                    _votedFor = null;
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.StartElection, Id, _state, _currentTerm));

                    //vote for self
                    ReceiveVote(
                        new RequestVoteResult {
                            Term = _currentTerm,
                            VoteGranted = true,
                            VoterId = Id,
                        }

                    );
                    _votedFor  = Id;

                    var token = _taskCancellation.Token;

                    //TODO error handling, cancellation 
                    var pendingRequests = new List<Task>(
                        _servers.Select(
                            s=>RequestAndReceiveVote(s, token)
                        )
                    );
                }
                //wait timeout period
                await _planner.ElectionDelay();

                //if we are still in candidate state, run another election
                //no need to cancel inflight vote requests because increment to term means they will be ignored
            }
        }

        public void Dispose()
        {
            lock(_lock)
            {
                CancelScheduledEvents();
            }
        }
    }
}