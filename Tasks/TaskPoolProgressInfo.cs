using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PerrysNetConsole;

namespace logsplit.Tasks
{
    public class TaskPoolProgressInfo
    {
        public int ThreadCount { get; set; }
        public int TaskCount { get; set; }
        public ConcurrentDictionary<string, ITaskProgress> States { get; set; }
        public CancellationTokenSource CancelationToken { get; set; }
        public Task InfoTask { get; set; }

        public TaskPoolProgressInfo(int threadCount, int taskCount)
        {
            this.ThreadCount = threadCount;
            this.TaskCount = taskCount;
            this.States = new ConcurrentDictionary<string, ITaskProgress>(this.ThreadCount * 2, this.TaskCount);
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

                    var finished = this.States.Values.Where(v => v.Status == TaskStatus.Finished);
                    if (finished.Any())
                    {
                        CoEx.WriteLine($"{finished.Count()} tasks are already finished.");
                        linesPresent++;
                    }

                    foreach(var stateCategory in this.States.Values.Where(v => v.Status != TaskStatus.Finished).OrderBy(v => v.Category).GroupBy(v => v.Category))
                    {
                        CoEx.WriteLine();
                        CoEx.WriteTitle($" {stateCategory.Key} ");
                        linesPresent += 2;

                        foreach(var stateItem in stateCategory.OrderByDescending(v => v.Status).ThenBy(v => v.Name))
                        {
                            CoEx.WriteLine(stateItem.ToString());
                            linesPresent++;
                        }
                    }

                    Thread.Sleep(500);
                }
            }, this.CancelationToken.Token);
        }

        public void Cancel()
        {
            this.CancelationToken.Cancel();
        }

        public void Update(ITaskProgress update)
        {
            this.States.AddOrUpdate(update.Name, update, (key, value) => value);
        }
    }
}
