using System.IO;

namespace logsplit.Tasks
{
    public class GZipWriterProgress : ITaskProgress
    {
        public string Name { get; set; }

        public TaskStatus Status { get; set; } = TaskStatus.NotStarted;

        public string Category { get; set; }

        public string Message { get; set; }

        public long LineCount { get; set; }

        public override string ToString()
        {
            return $"[{this.Status}] {Path.GetFileName(this.Name)}: {this.Message}, {this.LineCount:#,##0} lines written";
        }
    }
}
