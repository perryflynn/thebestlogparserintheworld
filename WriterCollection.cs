using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using logsplit.Extensions;
using Newtonsoft.Json;

namespace logsplit
{
    public class WriterCollection : IDisposable
    {
        private List<StreamInfo<StreamWriter>> OutputStreams = new List<StreamInfo<StreamWriter>>();

        public string OutputPath { get; set; }

        public WriterCollection(string outputLogPath)
        {
            this.OutputPath = outputLogPath;
        }

        public void Dispose()
        {
            if (this.OutputStreams != null)
            {
                this.OutputStreams.ForEach(s =>
                {
                    s.Dispose();

                    // remove the ".new" prefix from output filename
                    File.Move(s.TemporaryFullFileNameGz, s.FullFileNameGz, true);

                    // delete analyzer result if the log collection was changed
                    if (File.Exists($"{s.FullFileNameGz}.json"))
                    {
                        File.Delete($"{s.FullFileNameGz}.json");
                    }
                });

                this.OutputStreams.Clear();
                this.OutputStreams = null;
            }
        }

        public Task FlushAsync()
        {
            var allTasks = this.OutputStreams.Select(s => s.StreamWriter.FlushAsync()).ToArray();
            return Task.WhenAll(allTasks);
        }

        public void Flush()
        {
            this.FlushAsync().Wait();
        }

        public StreamInfo<StreamWriter> GetOutputStream(LogInfo info)
        {
            var existing = this.OutputStreams.SingleOrDefault(s => s.Equals(info));

            if (existing == null)
            {
                // stream not existing in our list, so create one
                existing = new StreamInfo<StreamWriter>()
                {
                    Path = this.OutputPath,
                    HostName = info.CollectionHostName,
                    LogGroup = info.CollectionGroupName,
                    Year = info.CollectionYear,
                    Month = info.CollectionMonth
                };

                // save loginfo
                if (!File.Exists($"{existing.FullFileNameGz}.loginfo.json"))
                {
                    var jsonFile = $"{existing.FullFileNameGz}.loginfo.json";
                    var json = JsonConvert.SerializeObject(info, Formatting.Indented);
                    File.WriteAllText(jsonFile, json);
                }

                // File Streams
                var fileStream = File.OpenWrite(existing.TemporaryFullFileNameGz);
                var gzipFileStream = new GZipStream(fileStream, CompressionMode.Compress, false);

                // Writer
                existing.StreamWriter = new StreamWriter(gzipFileStream, Encoding.UTF8);

                // copy existing data into new gz file
                if (File.Exists(existing.FullFileNameGz))
                {
                    using(var oldFileStream = File.OpenRead(existing.FullFileNameGz))
                    using(var oldGzStream = new GZipStream(oldFileStream, CompressionMode.Decompress, false))
                    {
                        foreach (var line in oldGzStream.ReadLineToEnd())
                        {
                            existing.StreamWriter.WriteLine(line);
                        }
                    }

                    // Flush existing data into new file
                    existing.StreamWriter.Flush();
                }

                // Add stream to our list
                this.OutputStreams.Add(existing);
            }

            return existing;
        }
    }
}