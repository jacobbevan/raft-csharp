using System;
using System.Threading.Tasks;

namespace Raft.Server
{
    public sealed class Planner : IPlanner
    {
        private Random _rand = new Random();
        private int _electionTimerMeanMs = 500;
        private int _electionTimerRangeMs = 200;
        private int _heartBeatIntervalMs = 200;

        public Planner(int electionTimerMeanMs, int electionTimerRangeMs, int heartBeatIntervalMs)
        {
            _electionTimerMeanMs = electionTimerMeanMs;
            _electionTimerRangeMs = electionTimerRangeMs;
            _heartBeatIntervalMs = heartBeatIntervalMs;
        }

        public Task ElectionDelay()
        {
            return Task.Delay(_rand.Next(_electionTimerMeanMs - _electionTimerRangeMs  / 2 , _electionTimerMeanMs + _electionTimerRangeMs / 2));
        }

        public Task HeatbeatDelay()
        {
            return Task.Delay(_heartBeatIntervalMs / 2);
        }
    }
}