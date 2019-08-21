
namespace Raft.Messages
{
    public class AppendEntriesCommand
    {
        public int Term { get; set; }
        public string LeaderId { get; set; }
    }
}