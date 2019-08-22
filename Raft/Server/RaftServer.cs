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
    public class RaftServer : IServer
    {


        public enum RaftServerState
        {
            Leader,
            Follower,
            Candidate
        }



        private object _lock = new object();
        private volatile RaftServerState _state = RaftServerState.Follower;

        private int _currentTerm;
        private string _votedFor;
        private CommandLog _log;
        private IAuditLog _auditLog;
        private IPlanner _planner;
        private int _votesReceived = 0;
        private IList<IServerProxy> _servers;
        private CancellationTokenSource _followerTimeOutTcs; 
        public string Id { get; private set; }
        public RaftServerState State {get {return _state;}}
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

                //TODO delete
                /*
                if(!BecomeFollowerIfTermIsStale(request.Term))
                {
                    switch(_state)
                    {
                        case  RaftServerState.Candidate:
                            BecomeFollower(request.Term);
                            break;
                        case RaftServerState.Follower:
                            Task.Run(ResetFollowerTimeOut);
                            break;
                    }


                }
                */

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
            lock(_lock)
            {
                if(StaleTermCheck(termToCompare))
                {
                    BecomeFollower(termToCompare);
                    return true;
                }
                return false;
            }
        }

        private void BecomeLeader()
        {
            lock(_lock)
            {
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeLeader, Id, _state, _currentTerm));
                _state = RaftServerState.Leader;
                Task.Run(SendHeartbeat);
            }
        }

        private async Task SendHeartbeat()
        {
            List<IServerProxy> servers;

            lock(_lock)
            {
                servers = new List<IServerProxy>(_servers);
            }

            _auditLog.LogRecord(new AuditRecord(AuditRecordType.SendHeartbeat, Id, _state, _currentTerm));
            var heartBeatCmd = new AppendEntriesCommand
            {
                Term = _currentTerm,
                LeaderId = Id

            };

            while(_state == RaftServerState.Leader)
            {
                //TODO error handling, cancellation
                var pendingRequests = new List<Task>(
                    servers.Select(
                        s=>s.AppendEntries(heartBeatCmd).ContinueWith(
                            t=>HandleHeartbeatResponse(t.Result)
                        )
                    )
                );

                await _planner.HeatbeatDelay();
            }
        }

        private void HandleHeartbeatResponse(AppendEntriesResult response)
        {
            lock(_lock)
            {
                _auditLog.LogRecord(new AuditRecord(AuditRecordType.RecHeartbeatResponse, Id, _state, _currentTerm));
                BecomeFollowerIfTermIsStale(response.Term);
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
        public void BecomeFollower(int term)
        {
            lock(_lock)
            {
                UpdateTerm(term);

                if(_state != RaftServerState.Follower)
                {
                    _state = RaftServerState.Follower;
                    _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeFollower, Id, _state, _currentTerm));
                }
                Task.Run(ResetFollowerTimeOut);
            }
        }

        private async Task ResetFollowerTimeOut()
        {
            var tcs = new CancellationTokenSource();
            var token = tcs.Token;

            //TODO this is not nice, fix it
            
            lock(_lock)
            {
                //TODO  error handling
                if(_followerTimeOutTcs != null)
                {
                    //are both necessary?
                    _followerTimeOutTcs.Cancel();
                    _followerTimeOutTcs.Dispose();
                }
                _followerTimeOutTcs = tcs;
            }

            await _planner.ElectionDelay();  

            if(!token.IsCancellationRequested && _state == RaftServerState.Follower)
            {
                await BecomeCandidate();           
            }
        } 

        public async Task BecomeCandidate()
        {            
            _state = RaftServerState.Candidate;

            _auditLog.LogRecord(new AuditRecord(AuditRecordType.BecomeCandidate, Id, _state, _currentTerm));
            while(_state == RaftServerState.Candidate)
            {
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

                //TODO error handling, cancellation 
                var pendingRequests = new List<Task>(
                    _servers.Select(
                        s=>s.RequestVote(
                            new RequestVoteCommand {
                                CandidateId = Id,
                                CurrentTerm = _currentTerm 
                            }
                        ).ContinueWith(
                            t=>ReceiveVote(t.Result)
                        )
                    )
                );

                //wait timeout period
                await _planner.ElectionDelay();

                //if we are still in candidate state, run another election
            }
        }
    }
}