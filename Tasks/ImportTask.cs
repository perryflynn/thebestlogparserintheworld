using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using logsplit.Extensions;
using Newtonsoft.Json;

namespace logsplit.Tasks
{
    public class ImportTask : ITaskPoolTask<ReadFileProgress, bool>
    {
        public string OutputFolder { get; set; }
        public string InputFile { get; set; }
        public GZipWriterCollection<StreamInfo> Writers { get; set; }

        public ImportTask(string outputFolder, string inputFile, GZipWriterCollection<StreamInfo> writers)
        {
            this.OutputFolder = outputFolder;
            this.InputFile = inputFile;
            this.Writers = writers;
        }

        public bool Execute(IProgress<ReadFileProgress> progressUpdater)
        {
            var progress = new ReadFileProgress()
            {
                Category = "Import",
                Status = TaskStatus.NotStarted,
                Name = this.InputFile,
                StartTime = DateTime.Now,
            };

            progressUpdater.Report(progress);

            progress.Status = TaskStatus.Running;
            progress.FileSizeBytes = this.InputFile.GetRealSize();
            progressUpdater.Report(progress);

            // loginfo
            var logInfoCache = new List<string>();
            var loginfoFile = Path.Combine(Path.GetDirectoryName(this.InputFile), "loginfo.json");
            var logInfo = JsonConvert.DeserializeObject<LogInfo>(File.ReadAllText(loginfoFile));

            // extract meta data from file name
            var fileInfoMatch = logInfo.FilenameRegex.Match(this.InputFile);
            var logGroup = fileInfoMatch.Groups["GroupName"].Value;
            var hostName = fileInfoMatch.Groups["HostName"].Value;

            logInfo.CollectionGroupName = logGroup;
            logInfo.CollectionHostName = hostName;

            using(var stream = this.InputFile.OpenFile())
            {
                foreach(string line in stream.ReadLineToEnd())
                {
                    // parse entry timestamp
                    if(Timeslot.TryGetTimestamp(logInfo, line, out Match regexMatch, out DateTime date))
                    {
                        logInfo.CollectionYear = date.Year;
                        logInfo.CollectionMonth = date.Month;

                        var streamInfo = new StreamInfo(this.OutputFolder, logInfo);

                        // save loginfo
                        if (!logInfoCache.Contains(streamInfo.FullFileNameGz) &&
                            !File.Exists($"{streamInfo.FullFileNameGz}.loginfo.json"))
                        {
                            var jsonFile = $"{streamInfo.FullFileNameGz}.loginfo.json";
                            var json = JsonConvert.SerializeObject(logInfo, Formatting.Indented);
                            File.WriteAllText(jsonFile, json);
                            logInfoCache.Add(streamInfo.FullFileNameGz);
                        }

                        // write log entry
                        this.Writers.WriteLine(streamInfo, line);

                        progress.LineCountValid++;
                    }
                    else
                    {
                        progress.LineCountInvalid++;
                    }

                    progress.CurrentOffsetBytes += line.Length;
                    progress.LineCount++;
                    progressUpdater.Report(progress);
                }
            }

            progress.Status = TaskStatus.Finished;
            progress.EndTime = DateTime.Now;
            progressUpdater.Report(progress);

            return true;
        }
    }
}
