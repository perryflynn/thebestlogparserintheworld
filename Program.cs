﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using logsplit.Extensions;
using PerrysNetConsole;
using Newtonsoft.Json;

/**
 * Analyse Stats:
 * 410M rows
 * ~ 58 Minutes Processing
 * = ~116k rows per second
 */

// ^(?<ClientIP>[^\s]+)\s+-\s+(?<ClientUser>[^\s]+)\s+\[(?<Timestamp>[^\]]+)\]\s+"(?:(?:(?<RequestMethod>[A-Z]+)\s+(?<RequestUri>[^\s]+)\s+(?<Protocol>[^"]+)|(?<InvalidRequest>.+)))?"\s+(?<StatusCode>[0-9]+)\s+(?<BytesSent>[0-9]+)\s+"(?<Referer>[^"]*)"\s+"(?<UserAgent>[^"]*)"\s+"[^"]+"\s+"[^"]+"$

namespace logsplit
{
    public class Program
    {
        // Log Splitter

        private static readonly string logPath = @"/run/user/1000/gvfs/sftp:host=ellen,user=christian/home/christian/Download/logs";
        private static readonly string outputLogPath = @"/run/user/1000/gvfs/sftp:host=ellen,user=christian/home/christian/Download/processed-logs";
        private static List<StreamInfo<StreamWriter>> OutputStreams = new List<StreamInfo<StreamWriter>>();


        public static void Main(string[] args)
        {
            CoEx.ColorTitlePrimary = new ColorScheme(ConsoleColor.Gray, ConsoleColor.Black);
            CoEx.ColorTitleSecondary = new ColorScheme(ConsoleColor.DarkBlue, ConsoleColor.Black);

            // Split new logfiles into month-collections
            MainSplitAccessLogs();

            // Analyze Logfile
            MainAnalyzeAccessLog();

            // Create stats
            var logRgx = new Regex(@"-dingetun-.+\.log\.gz\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var logFiles = Directory
                .EnumerateFiles(outputLogPath, "*.*", SearchOption.AllDirectories)
                .Where(file => logRgx.IsMatch(file))
                .OrderBy(file => file);

            MainResults(logFiles.ToArray());
        }

        /// <summary>
        /// Split new log files processed by logrotate into
        /// our monthly collections
        /// </summary>
        private static void MainSplitAccessLogs()
        {
            CoEx.Clear();
            CoEx.WriteTitleLarge("Split Log Files into monthly collections");

            // Find valid log files
            var logFiles = Directory
                .EnumerateFiles(logPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".gz", true, CultureInfo.InvariantCulture))
                .OrderBy(file => file)
                .ToArray();

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
                long max = GetRealSize(logFile);

                // get meta names
                var logGroup = fileInfoMatch.Groups["GroupName"].Value;
                var hostName = fileInfoMatch.Groups["HostName"].Value;

                using(var stream = OpenFile(logFile))
                {
                    // process lines
                    foreach(string line in ReadToEnd(stream))
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
                            GetOutputStream(logInfo).StreamWriter.WriteLine(line);
                        }

                        // update progress
                        current += line.Length;
                        progress.Update(current, max);
                    }
                }

                // flush output stream and delete source file
                Flush().Wait();
                File.Delete(logFile);

                // reset for next file
                progress.Dispose();
                CoEx.Seek(0, -2, true);
            }

            // finalize output streams
            OutputStreams.ForEach(s =>
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

            OutputStreams.Clear();
        }

        /// <summary>
        /// Analyze monthly collections
        /// </summary>
        /// <param name="files"></param>
        private static void MainAnalyzeAccessLog()
        {
            CoEx.Clear();
            CoEx.WriteTitleLarge("Analyze Log Files");

            // find unprocessed log collections
            var logFiles = Directory
                .EnumerateFiles(outputLogPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".log.gz", true, CultureInfo.InvariantCulture))
                .Where(file => !File.Exists($"{file}.json"))
                .OrderBy(file => file)
                .ToArray();

            var startTime = DateTime.Now;

            foreach(var file in logFiles)
            {
                Dictionary<DateTime, Timeslot> counter = new Dictionary<DateTime, Timeslot>();

                CoEx.WriteLine($"[{Array.IndexOf(logFiles, file)+1}/{logFiles.Length}] {Path.GetFileName(file)}\n");

                var progress = new Progress();
                progress.Start();

                // load loginfo
                var logInfo = JsonConvert.DeserializeObject<LogInfo>(File.ReadAllText($"{file}.loginfo.json"));

                // init progress counters
                long current = 0;
                long max = GetRealSize(file);

                // parse lines
                using(var stream = OpenFile(file))
                foreach(var line in ReadToEnd(stream))
                {
                    // match one line against our regex
                    var match = logInfo.LineRegex.Match(line);

                    if (match.Groups[logInfo.TimestampName].Success)
                    {
                        // get timestamp
                        var tsstr = match.Groups[logInfo.TimestampName].Value;
                        var tsOk = Timeslot.TryParseTimestamp(logInfo, tsstr, out DateTime ts);

                        if (tsOk)
                        {
                            // analyze
                            counter.EnsureField(
                                ts.Date,
                                () => new Timeslot() { Time = ts.Date },
                                dict => dict[ts.Date].ParseHit(logInfo, match, line)
                            );
                        }
                    }

                    // update counters
                    current += line.Length;
                    progress.Update(current, max);
                }

                // save result as json
                File.WriteAllText($"{file}.json", JsonConvert.SerializeObject(counter));

                // reset for next file
                progress.Dispose();
                CoEx.Seek(0, -2, true);
            }

            CoEx.WriteLine();
            Console.WriteLine($"Took {(DateTime.Now - startTime).TotalSeconds:0.000} seconds");
        }

        private static void MainResults(params string[] files)
        {
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
                .GroupBy(c => $"{c.Key.Year:0000}-{c.Key.Month:00}")
                .ToDictionary(c => c.Key, c => c.Sum(i => (double)i.Value.VisitorsCount));

            load.Stop();
            graph.Draw(visitorsGraphData);

            CoEx.WriteLine();
            CoEx.WriteTitle("Hits");
            CoEx.WriteLine();
            load.Start();

            var hitGraphData = counter
                .GroupBy(c => $"{c.Key.Year:0000}-{c.Key.Month:00}")
                .ToDictionary(c => c.Key, c => c.Sum(i => (double)i.Value.Hits));

            load.Stop();
            graph.Draw(hitGraphData);

        }

        /// <summary>
        /// Open a standard or compressed stream depending on the file extension
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static Stream OpenFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var fileStream = File.OpenRead(filePath);

                if (filePath.EndsWith(".gz", true, CultureInfo.InvariantCulture))
                {
                    return new GZipStream(fileStream, CompressionMode.Decompress, false);
                }

                return fileStream;
            }

            throw new Exception("File does not exists");
        }

        /// <summary>
        /// Get real size of a gzipped file
        /// </summary>
        /// <param name="file">The file</param>
        /// <returns>Real size in bytes</returns>
        private static long GetRealSize(string file)
        {
            if (file.EndsWith(".gz", true, CultureInfo.InvariantCulture))
            {
                // get real size from a gzipped file
                using(var fs = File.OpenRead(file))
                {
                    fs.Position = fs.Length - 4;
                    var b = new byte[4];
                    fs.Read(b, 0, 4);
                    return BitConverter.ToUInt32(b, 0);
                }
            }
            else
            {
                // get size of a normal file
                return (new FileInfo(file)).Length;
            }
        }

        /// <summary>
        /// Read a stream line by line to the end
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>Enumerator for loops etc</returns>
        private static IEnumerable<string> ReadToEnd(Stream stream)
        {
            using(var reader = new StreamReader(stream, Encoding.UTF8, false, -1, false))
            {
                while(reader.Peek() >= 0)
                {
                    yield return reader.ReadLine();
                }
            }
        }

        /// <summary>
        /// Create output streams depending on the metadata
        /// </summary>
        /// <param name="hostName">Hostname of the log</param>
        /// <param name="logGroup">Group/vHost of the log</param>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <returns>GZip Stream Writer</returns>
        private static StreamInfo<StreamWriter> GetOutputStream(LogInfo info)
        {
            var existing = OutputStreams.SingleOrDefault(s => s.Equals(info.CollectionHostName, info.CollectionGroupName, info.CollectionYear, info.CollectionMonth));

            if (existing == null)
            {
                // stream not existing in our list, so create one
                existing = new StreamInfo<StreamWriter>()
                {
                    Path = outputLogPath,
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
                        foreach (var line in ReadToEnd(oldGzStream))
                        {
                            existing.StreamWriter.WriteLine(line);
                        }
                    }

                    // Flush existing data into new file
                    existing.StreamWriter.Flush();
                }

                // Add stream to our list
                OutputStreams.Add(existing);
            }

            return existing;
        }

        /// <summary>
        /// Flush all output streams
        /// </summary>
        /// <returns>Async Task to wait for</returns>
        private static Task Flush()
        {
            var allTasks = OutputStreams.Select(s => s.StreamWriter.FlushAsync()).ToArray();
            return Task.WhenAll(allTasks);
        }
    }
}
