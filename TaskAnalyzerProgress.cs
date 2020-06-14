using System;

namespace logsplit
{
    public class TaskAnalyzerProgress
    {
        public string Name { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.NotStarted;
        public DateTime? StartTime { get; set; } = null;
        public DateTime? EndTime { get; set; } = null;
        public long FileSizeBytes { get; set; }
        public long CurrentOffsetBytes { get; set; }
        public long LineCount { get; set; }
        public long LineCountValid { get; set; }
        public long LineCountInvalid { get; set; }
    }
}
