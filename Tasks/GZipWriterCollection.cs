using System;
using System.Collections.Concurrent;

namespace logsplit.Tasks
{
    public class GZipWriterCollection<TKey> : IDisposable
        where TKey : IGZipWriterKey
    {
        public ConcurrentDictionary<TKey, GZipWriterTask> Writers { get; set; }
        public Progress<ITaskProgress> ProgressUpdate { get; set; }

        public GZipWriterCollection(int threadCount, int writerCount, Action<ITaskProgress> progressUpdate)
        {
            this.Writers = new ConcurrentDictionary<TKey, GZipWriterTask>(threadCount * 2, writerCount);
            this.ProgressUpdate = new Progress<ITaskProgress>(progressUpdate);;
        }

        public void Dispose()
        {
            foreach(var writer in this.Writers)
            {
                writer.Value.Dispose();
            }

            this.Writers.Clear();
            this.Writers = null;
        }

        public void WriteLine(TKey key, string line)
        {
            var writer = this.Writers.GetOrAdd(
                key,
                key => new GZipWriterTask(key.GetGZipWriterFileName(), this.ProgressUpdate, 128, true)
            );

            writer.AddLine(line);
        }
    }
}
