using System;

namespace logsplit
{
    public interface ITaskPoolTask<TProgress, TReturn>
    {
         TReturn Execute(IProgress<TProgress> progressUpdater);
    }
}
