using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace logsplit.Tasks
{
    public class TaskPool<TProgress, TReturn>
    {
        public int ThreadsConcurrentMax { get; private set; }
        public List<ITaskPoolTask<TProgress, TReturn>> Tasks { get; private set; } = new List<ITaskPoolTask<TProgress, TReturn>>();

        public TaskPool(int threadsMax)
        {
            this.ThreadsConcurrentMax = threadsMax;
        }

        public void Add(params ITaskPoolTask<TProgress, TReturn>[] tasks)
        {
            this.Tasks.AddRange(tasks);
        }

        public IEnumerable<TReturn> Execute(Action<TProgress> progressUpdate)
        {
            var progress = new Progress<TProgress>(progressUpdate);

            using (var semaphore = new SemaphoreSlim(this.ThreadsConcurrentMax))
            {
                var tasks = new List<Task<TReturn>>();

                foreach(var task in this.Tasks)
                {
                    semaphore.Wait();

                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            return task.Execute(progress);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                while (tasks.Count > 0)
                {
                    Task.WaitAny(tasks.ToArray());
                    var completedTasks = tasks.Where(t => t.IsCompleted).ToList();
                    tasks = tasks.Except(completedTasks).ToList();

                    foreach (var completedTask in completedTasks)
                    {
                        yield return completedTask.Result;
                    }
                }
            }
        }
    }
}
