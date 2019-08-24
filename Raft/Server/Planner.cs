using System;
using System.Threading;
using System.Threading.Tasks;

namespace Raft.Server
{
    public sealed class Planner : IPlanner
    {
        private Random _rand = new Random();
        private int _electionTimerMeanMs;
        private int _electionTimerRangeMs;
        private int _heartBeatIntervalMs;

        private int _retryIntervalMs;

        public Planner(int electionTimerMeanMs, int electionTimerRangeMs, int heartBeatIntervalMs, int retryIntervalMs)
        {
            _electionTimerMeanMs = electionTimerMeanMs;
            _electionTimerRangeMs = electionTimerRangeMs;
            _heartBeatIntervalMs = heartBeatIntervalMs;
            _retryIntervalMs = 50;
        }

        public Task ElectionDelay()
        {
            return Task.Delay(_rand.Next(_electionTimerMeanMs - _electionTimerRangeMs  / 2 , _electionTimerMeanMs + _electionTimerRangeMs / 2));
        }

        public Task HeatbeatDelay()
        {
            return Task.Delay(_heartBeatIntervalMs / 2);
        }

        public Task RetryDelay()
        {
            return Task.Delay(_retryIntervalMs);
        }
    }
}