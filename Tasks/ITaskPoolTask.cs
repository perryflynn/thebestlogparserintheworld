using System;

namespace logsplit.Tasks
{
    public interface ITaskPoolTask<TProgress, TReturn>
    {
         TReturn Execute(IProgress<TProgress> progressUpdater);
    }
}
