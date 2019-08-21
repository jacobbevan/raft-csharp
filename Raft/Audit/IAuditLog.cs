namespace Raft.Audit
{
    public interface IAuditLog
    {
        void LogRecord(AuditRecord record);
    }
}