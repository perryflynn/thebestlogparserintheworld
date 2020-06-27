using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using logsplit.Extensions;
using PerrysNetConsole;
using Newtonsoft.Json;
using CommandLine;
using logsplit.Tasks;

namespace logsplit
{
    public class Program
    {
        public static int Main(string[] args)
        {
            CoEx.ColorTitlePrimary = new ColorScheme(ConsoleColor.Gray, ConsoleColor.Black);
            CoEx.ColorTitleSecondary = new ColorScheme(ConsoleColor.DarkBlue, ConsoleColor.Black);

            return MainParseArguments(args);
        }

        private static int MainParseArguments(params string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<InitOptions, ImportOptions, AnalyzeOptions, StatisticOptions>(args)
                .MapResult(
                    (InitOptions opts) => MainInit(opts),
                    (ImportOptions opts) => MainImport(opts),
                    (AnalyzeOptions opts) => MainAnalyze(opts),
                    (StatisticOptions opts) => MainStatistic(opts),
                    errs => 1
                );
        }

        private static int MainInit(InitOptions opts)
        {
            if (string.IsNullOrWhiteSpace(opts.Path) || !opts.Path.Exists())
            {
                CoEx.WriteLine("The path does not exists.");
                return 1;
            }

            if (!opts.Path.IsDirectory())
            {
                CoEx.WriteLine("The path is not a directory.");
                return 1;
            }

            if (!opts.Path.IsEmptyDirectory("input", "repository"))
            {
                CoEx.WriteLine("The directory is not empty.");
                return 1;
            }

            var inputFolder = Path.Combine(opts.Path, "input");
            var repositoryFolder = Path.Combine(opts.Path, "repository");

            if (!Directory.Exists(inputFolder))
            {
                Directory.CreateDirectory(inputFolder);
            }

            if (!Directory.Exists(repositoryFolder))
            {
                Directory.CreateDirectory(repositoryFolder);
            }

            if (!string.IsNullOrWhiteSpace(opts.HostFolder))
            {
                var hostFolder = Path.Combine(inputFolder, opts.HostFolder);
                var hostCfg = Path.Combine(hostFolder, "loginfo.json");

                if (!Directory.Exists(hostFolder))
                {
                    Directory.CreateDirectory(hostFolder);
                }

                if (!File.Exists(hostCfg))
                {
                    File.WriteAllText(hostCfg, JsonConvert.SerializeObject(new LogInfo(), Formatting.Indented));
                }
            }

            return 0;
        }

        /// <summary>
        /// Split new log files processed by logrotate into
        /// our monthly collections
        /// </summary>
        private static int MainImport(ImportOptions opts)
        {
            CoEx.Clear();
            CoEx.WriteTitleLarge("Split Log Files into monthly collections");

            var startTime = DateTime.Now;
            var inputPath = Path.Combine(opts.Path, "input");
            var outputPath = Path.Combine(opts.Path, "repository");

            // Find valid log files
            var logFiles = Directory
                .EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".gz", true, CultureInfo.InvariantCulture))
                .OrderBy(file => file)
                .ToArray();

            // Abort import when there are dangling *.new files
            var danglingFiles = Directory
                .EnumerateFiles(outputPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".gz.new", true, CultureInfo.InvariantCulture));

            if (logFiles.Any() && danglingFiles.Any())
            {
                CoEx.WriteLine($"The output directory '{outputPath}' contains " +
                    "unfinished *.new files. Please rename or delete them.");

                return 1;
            }

            // Perform import
            using(var writers = new WriterCollection(outputPath))
            {
                foreach(var logFile in logFiles)
                {
                    CoEx.WriteLine($"[{Array.IndexOf(logFiles, logFile)+1}/{logFiles.Length}] {Path.GetFileName(logFile)}\n");

                    // log metadata
                    var loginfoFile = Path.Combine(Path.GetDirectoryName(logFile), "loginfo.json");
                    var logInfo = JsonConvert.DeserializeObject<LogInfo>(File.ReadAllText(loginfoFile));

                    var fileInfoMatch = logInfo.FilenameRegex.Match(logFile);

                    // init progress counters
                    var progress = new Progress();
                    progress.Start();

                    long current = 0;
                    long max = logFile.GetRealSize();

                    // get meta names
                    var logGroup = fileInfoMatch.Groups["GroupName"].Value;
                    var hostName = fileInfoMatch.Groups["HostName"].Value;

                    using(var stream = logFile.OpenFile())
                    {
                        // process lines
                        foreach(string line in stream.ReadLineToEnd())
                        {
                            // extract date from line
                            var lineParts = line.Split(new char[] { '[', ']' });

                            if(Timeslot.TryParseTimestamp(logInfo, lineParts[1], out DateTime date))
                            {
                                logInfo.CollectionGroupName = logGroup;
                                logInfo.CollectionHostName = hostName;
                                logInfo.CollectionYear = date.Year;
                                logInfo.CollectionMonth = date.Month;

                                // push line into correct output stream
                                progress.IsWaiting = true;
                                var writer = writers.GetOutputStream(logInfo).StreamWriter;
                                progress.IsWaiting = false;

                                writer.WriteLine(line);
                            }

                            // update progress
                            current += line.Length;
                            progress.Update(current, max);
                        }
                    }

                    // flush output stream and delete source file
                    writers.Flush();

                    // reset for next file
                    progress.Dispose();
                    CoEx.Seek(0, -2, true);
                }
            }

            // delete imported files
            foreach(var logFile in logFiles)
            {
                if (File.Exists(logFile))
                {
                    File.Delete(logFile);
                }
            }

            CoEx.WriteLine();
            CoEx.WriteLine($"Took {(DateTime.Now - startTime).TotalSeconds:0.000} seconds");

            return 0;
        }

        /// <summary>
        /// Analyze monthly collections
        /// </summary>
        /// <param name="files"></param>
        private static int MainAnalyze(AnalyzeOptions opts)
        {
            CoEx.Clear();
            CoEx.WriteTitleLarge("Analyze Log Files");
            CoEx.WriteLine();

            // source
            var repoPath = Path.Combine(opts.Path, "repository");

            Regex filePattern = null;
            if (!string.IsNullOrWhiteSpace(opts.FilePattern))
            {
                filePattern = new Regex(opts.FilePattern, RegexOptions.Compiled);
            }

            // find unprocessed log collections
            var logFiles = Directory
                .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".log.gz", true, CultureInfo.InvariantCulture))
                .Where(file => filePattern == null || filePattern.IsMatch(file))
                .Where(file => opts.Force || !File.Exists($"{file}.json"))
                .OrderBy(file => file)
                .ToArray();

            // Initialize pool
            var numThreads = opts.Cpus;
            if (numThreads < 1)
            {
                numThreads = 1;
            }

            var threadPool = new TaskPool<TaskAnalyzerProgress, bool>(numThreads);

            foreach(var file in logFiles)
            {
                var logInfo = JsonConvert.DeserializeObject<LogInfo>(File.ReadAllText($"{file}.loginfo.json"));
                var task = new AnalyzerTask(logInfo, file);

                threadPool.Add(task);
            }

            // Initialize thread statistics
            var progress = new TaskPoolProgressInfo<TaskAnalyzerProgress>(numThreads, logFiles.Length);

            // Execute threads
            progress.Start();
            threadPool.Execute(update =>
            {
                // store thread stats
                progress.Update(update);
            })
            .Count();

            // cancel stats thread
            progress.Cancel();

            // print summary
            CoEx.WriteLine();

            var startTime = progress.States.Min(v => v.Value.StartTime.Value);
            var endTime = progress.States.Max(v => v.Value.EndTime.Value);
            var totalSeconds = (endTime - startTime).TotalSeconds;
            var totalLines = progress.States.Sum(v => v.Value.LineCount);

            CoEx.WriteLine($"Took {totalSeconds:#,##0.###} seconds ({(totalSeconds/60):#,##0.###} minutes)");
            CoEx.WriteLine($"Processed {progress.States.Count:#,##0} files");
            CoEx.WriteLine($"Processed {totalLines:#,##0} lines");
            CoEx.WriteLine($"Processed {(totalLines / totalSeconds):#,##0.###} lines per second");

            return 0;
        }

        private static int MainStatistic(StatisticOptions opts)
        {
            CoEx.Clear();
            CoEx.WriteTitleLarge("Analyze Log Files");

            var repoPath = Path.Combine(opts.Path, "repository");
            var rgx = new Regex(opts.FilePattern, RegexOptions.Compiled);

            // find unprocessed log collections
            var files = Directory
                .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".log.gz.json", true, CultureInfo.InvariantCulture))
                .Where(file => rgx.IsMatch(Path.GetFileName(file)))
                .OrderBy(file => file)
                .ToArray();

            var graph = new SimpleGraph();
            var load = new LoadIndicator() { Message = "Calculate..." };
            Dictionary<DateTime, Timeslot> counter = new Dictionary<DateTime, Timeslot>();

            using(var progress = new Progress())
            {
                progress.Start();

                foreach(var file in files)
                {
                    var counterPart = JsonConvert.DeserializeObject<Dictionary<DateTime, Timeslot>>(File.ReadAllText(file));
                    foreach(var counterItem in counterPart)
                    {
                        counter.Add(counterItem.Key, counterItem.Value);
                    }

                    progress.Update(Array.IndexOf(files, file), files.Length);
                }
            }

            CoEx.Clear();
            CoEx.WriteTitleLarge("Perrys Access Log Analyzer");
            CoEx.WriteLine();

            CoEx.WriteLine($"Time range: {counter.Values.Min(c => c.Time):yyyy-MM-dd} - {counter.Values.Max(c => c.Time):yyyy-MM-dd}");
            CoEx.WriteLine($"Total Hits: {counter.Values.Sum(c => c.Hits):0,000}");
            CoEx.WriteLine($"Total Visitors: {counter.Values.Sum(c => c.VisitorsCount):0,000}");
            CoEx.WriteLine();

            CoEx.WriteTitle("Visitors");
            CoEx.WriteLine();
            load.Start();

            var visitorsGraphData = counter
                .GroupBy(c => $"{c.Key.Year:0000}-{c.Key.GetIso8601WeekOfYear():00}")
                .ToDictionary(c => c.Key, c => c.Sum(i => (double)i.Value.VisitorsCount));

            load.Stop();
            graph.Draw(visitorsGraphData);

            CoEx.WriteLine();
            CoEx.WriteTitle("Hits");
            CoEx.WriteLine();
            load.Start();

            var hitGraphData = counter
                .GroupBy(c => $"{c.Key.Year:0000}-{c.Key.GetIso8601WeekOfYear():00}")
                .ToDictionary(c => c.Key, c => c.Sum(i => (double)i.Value.Hits));

            load.Stop();
            graph.Draw(hitGraphData);

            CoEx.WriteLine();
            CoEx.WriteTitle("Referers");
            CoEx.WriteLine();

            var refererList = counter
                .SelectMany(c => c.Value.RefererList)
                .GroupBy(c => c.Value)
                .Select(c => new { Name = c.Key, Count = c.Sum(v => v.Count) })
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Name)
                .Select(c => new string[] { c.Count.ToString("0,000"), c.Name })
                .Take(100)
                .ToArray();

            CoEx.WriteTable(RowCollection.Create(refererList));

            var reqsplit = new Regex(@"/(?:[^/]+\.html|[^/.]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var reqgroups = new Tuple<string, Func<string,bool>>[] {
                new Tuple<string, Func<string,bool>>(@"'/(?:[^/]+\.html|[^/.]+)?$'", (string name) => reqsplit.IsMatch(name)),
                new Tuple<string, Func<string,bool>>(@"NOT '/(?:[^/]+\.html|[^/.]+)?$'", (string name) => !reqsplit.IsMatch(name)),
            };

            foreach(var pattern in reqgroups)
            {
                CoEx.WriteLine();
                CoEx.WriteTitle($"Requests equals {pattern.Item1}");
                CoEx.WriteLine();

                var requestsList = counter
                    .SelectMany(c => c.Value.RequestList)
                    .Where(c => ((int)(c.HttpStatus/100)) == 2)
                    .GroupBy(c => $"{c.HttpStatus} {c.Method} {c.Url}")
                    .Select(c => new { Url = c.First().Url, Method = c.First().Method, Status = c.First().HttpStatus, Count = c.Sum(v => v.Count) })
                    .Where(c => pattern.Item2(c.Url))
                    .OrderByDescending(c => c.Count)
                    .Select(c => new string[] { c.Count.ToString("0,000"), c.Method, c.Status.ToString(), c.Url })
                    .Take(100)
                    .ToArray();

                CoEx.WriteTable(RowCollection.Create(requestsList));
            }

            return 0;
        }
    }
}
