namespace logsplit.Tasks
{
    public interface ITaskProgress
    {
         string Name { get; }
         TaskStatus Status { get; }

         string ToString();
    }
}
