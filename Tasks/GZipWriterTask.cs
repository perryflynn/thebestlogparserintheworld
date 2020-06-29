using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using logsplit.Extensions;

namespace logsplit.Tasks
{
    public class GZipWriterTask : IDisposable
    {
        public string FileName { get; private set; }
        public BlockingCollection<string> Buffer { get; private set; }
        public Task WriterTask { get; set; }
        public CancellationTokenSource TaskToken { get; private set; }
        public IProgress<ITaskProgress> Progress { get; private set; }

        public GZipWriterTask(string fileName, IProgress<ITaskProgress> progressUpdater, int bufferSize = 128, bool append = false)
        {
            this.FileName = fileName;
            this.Buffer = new BlockingCollection<string>(bufferSize);
            this.TaskToken = new CancellationTokenSource();
            this.Progress = progressUpdater;
            this.Start();
        }

        public void Dispose()
        {
            // empty buffer
            if (this.Buffer != null)
            {
                this.Buffer.CompleteAdding();
            }

            // cancel task
            if (this.WriterTask != null && this.WriterTask.IsCanceled == false && this.WriterTask.IsCompleted == false &&
                this.TaskToken.IsCancellationRequested == false)
            {
                this.TaskToken.Cancel();
                this.WriterTask.Wait();
                this.TaskToken = null;
                this.WriterTask = null;
            }

            // dispose buffer
            if (this.Buffer != null)
            {
                this.Buffer.Dispose();
                this.Buffer = null;
            }

            // rename file
            if (File.Exists($"{this.FileName}.new"))
            {
                File.Move($"{this.FileName}.new", this.FileName, true);
            }
        }

        public void AddLine(string line)
        {
            this.Buffer.Add(line);
        }

        public void AddLine(string line, CancellationToken cancellationToken)
        {
            this.Buffer.Add(line, cancellationToken);
        }

        private void Start()
        {
            this.WriterTask = Task.Run(() =>
            {
                var tempName = $"{this.FileName}.new";

                var progress = new GZipWriterProgress()
                {
                    Status = TaskStatus.NotStarted,
                    Name = this.FileName,
                    Category = "Writer",
                    Message = "Initializing",
                };

                this.Progress.Report(progress);

                // open output file
                using(var fileStream = File.OpenWrite(tempName))
                using(var gzipFileStream = new GZipStream(fileStream, CompressionMode.Compress, false))
                using(var writer = new StreamWriter(gzipFileStream, Encoding.UTF8))
                {
                    // copy existing file info new output file
                    if (File.Exists(this.FileName))
                    {
                        progress.Message = "Import existing data into new GZip file";
                        this.Progress.Report(progress);

                        using(var oldFileStream = File.OpenRead(this.FileName))
                        using(var oldGzStream = new GZipStream(oldFileStream, CompressionMode.Decompress, false))
                        {
                            foreach (var line in oldGzStream.ReadLineToEnd())
                            {
                                writer.WriteLine(line);

                                progress.LineCount++;
                                this.Progress.Report(progress);

                                if (this.TaskToken.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        }

                        writer.Flush();
                    }

                    progress.Message = "Listening for data";
                    progress.Status = TaskStatus.Running;
                    this.Progress.Report(progress);

                    // wait for buffer items and write them into output
                    try
                    {
                        while(this.TaskToken.IsCancellationRequested == false &&
                            this.Buffer.IsCompleted == false)
                        {
                            var line = this.Buffer.Take();
                            writer.WriteLine(line);

                            progress.LineCount++;
                            this.Progress.Report(progress);
                        }
                    }
                    catch(InvalidOperationException) { }
                    catch(OperationCanceledException) { }
                    finally
                    {
                        writer.Flush();
                    }
                }

                progress.Message = "Finializing file";
                progress.Status = TaskStatus.Finished;
                this.Progress.Report(progress);
            },
            this.TaskToken.Token);
        }
    }
}
