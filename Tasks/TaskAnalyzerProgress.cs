using System;
using System.IO;

namespace logsplit.Tasks
{
    public class TaskAnalyzerProgress : ITaskProgress
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

        public override string ToString()
        {
            var speed = "";
            if (this.StartTime.HasValue)
            {
                var end = this.EndTime.HasValue ? this.EndTime.Value : DateTime.Now;
                speed = $", {this.LineCount / (end - this.StartTime.Value).TotalSeconds:#,##0.0} lines/second";
            }

            return $"[{this.Status}] {Path.GetFileName(this.Name)}: {this.CurrentOffsetBytes:###,##0} byte, {this.LineCount:#,##0} lines{speed}";
        }
    }
}
