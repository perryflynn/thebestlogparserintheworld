using System;
using System.Collections.Generic;
using System.IO;
using logsplit.Extensions;
using Newtonsoft.Json;

namespace logsplit.Tasks
{
    public class AnalyzerTask : ITaskPoolTask<ReadFileProgress, bool>
    {
        public LogInfo LogInfo { get; set; }
        public string LogFile { get; set; }

        public AnalyzerTask(LogInfo logInfo, string logFile)
        {
            this.LogFile = logFile;
            this.LogInfo = logInfo;
        }

        public bool Execute(IProgress<ReadFileProgress> progressUpdater)
        {
            // Init progress
            var progress = new ReadFileProgress()
            {
                Category = "Analyze",
                Name = this.LogFile,
                StartTime = DateTime.Now,
            };

            progressUpdater.Report(progress);

            progress.FileSizeBytes = this.LogFile.GetRealSize();
            progress.Status = TaskStatus.Running;
            progressUpdater.Report(progress);

            // Begin parsing
            Dictionary<DateTime, Timeslot> counter = new Dictionary<DateTime, Timeslot>();

            using (var stream = this.LogFile.OpenFile())
            foreach (var line in stream.ReadLineToEnd())
            {
                // match one line against our regex
                var match = this.LogInfo.LineRegex.Match(line);

                if (match.Groups[this.LogInfo.TimestampName].Success)
                {
                    // get timestamp
                    var tsstr = match.Groups[this.LogInfo.TimestampName].Value;
                    var tsOk = Timeslot.TryParseTimestamp(this.LogInfo, tsstr, out DateTime ts);

                    if (tsOk)
                    {
                        // analyze
                        counter.EnsureField(
                            ts.Date,
                            () => new Timeslot() { Time = ts.Date },
                            dict => dict[ts.Date].ParseHit(this.LogInfo, match, line)
                        );

                        // stats
                        progress.LineCountValid++;
                    }
                    else
                    {
                        // stats
                        progress.LineCountInvalid++;
                    }
                }
                else
                {
                    // stats
                    progress.LineCountInvalid++;
                }

                // stats
                progress.LineCount++;
                progress.CurrentOffsetBytes += line.Length;

                progressUpdater.Report(progress);
            }

            // save result as json
            if (File.Exists($"{this.LogFile}.json"))
            {
                File.Delete($"{this.LogFile}.json");
            }

            var counterJson = JsonConvert.SerializeObject(counter, Formatting.Indented);
            File.WriteAllText($"{this.LogFile}.json", counterJson);

            // finish
            progress.EndTime = DateTime.Now;
            progress.Status = TaskStatus.Finished;
            progressUpdater.Report(progress);

            return true;
        }
    }
}
