using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PerrysNetConsole;

namespace logsplit.Tasks
{
    public class TaskPoolProgressInfo<TProgress>
        where TProgress : ITaskProgress
    {
        public int ThreadCount { get; set; }
        public int TaskCount { get; set; }
        public ConcurrentDictionary<string, TProgress> States { get; set; }
        public CancellationTokenSource CancelationToken { get; set; }
        public Task InfoTask { get; set; }

        public TaskPoolProgressInfo(int threadCount, int taskCount)
        {
            this.ThreadCount = threadCount;
            this.TaskCount = taskCount;
            this.States = new ConcurrentDictionary<string, TProgress>(this.ThreadCount * 2, this.TaskCount);
        }

        public void Start()
        {
            this.CancelationToken = new CancellationTokenSource();
            this.InfoTask = Task.Run(() =>
            {
                int linesPresent = 0;
                bool running = true;
                while(this.CancelationToken.IsCancellationRequested == false || running == true)
                {
                    if (this.CancelationToken.IsCancellationRequested)
                    {
                        // use this additional bool to ensure a final progress update
                        // after all tasks completed
                        running = false;
                    }

                    if (linesPresent > 0)
                    {
                        CoEx.Seek(0, 0 - linesPresent, true);
                    }

                    linesPresent = 0;
                    foreach(var stateItem in this.States.Values.OrderByDescending(v => v.Status).ThenBy(v => v.Name))
                    {
                        CoEx.WriteLine(stateItem.ToString());
                        linesPresent++;
                    }

                    Thread.Sleep(500);
                }
            }, this.CancelationToken.Token);
        }

        public void Cancel()
        {
            this.CancelationToken.Cancel();
        }

        public void Update(TProgress update)
        {
            this.States.AddOrUpdate(update.Name, update, (key, value) => value);
        }
    }
}