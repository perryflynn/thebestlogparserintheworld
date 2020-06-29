namespace logsplit.Tasks
{
    public interface ITaskProgress
    {
        string Category { get; }

        string Name { get; }

        TaskStatus Status { get; }

        string ToString();
    }
}
