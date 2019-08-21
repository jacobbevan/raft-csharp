namespace Raft.Messages
{
    public class AppendEntriesResult
    {
        public bool Success { get; internal set; }
        public int Term { get; internal set; }
    }
}