using System.Collections.Generic;

namespace Raft.Audit
{
    public class SimpleAuditLog : IAuditLog
    {
        private List<AuditRecord> _records = new List<AuditRecord>();
        public void LogRecord(AuditRecord record)
        {
            _records.Add(record);
        }
    }
}