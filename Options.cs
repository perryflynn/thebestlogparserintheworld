using System.IO;
using CommandLine;

namespace logsplit
{

    [Verb("init", HelpText = "Initialize a log repository")]
    public class InitOptions
    {
        [Option('d', "directory", HelpText = "Path to the new repository")]
        public string Path { get; set; } = Directory.GetCurrentDirectory();

        [Option('f', "host-folder", HelpText = "Create a new host folder")]
        public string HostFolder { get; set; } = null;
    }

    [Verb("import", HelpText = "Import log files into repository")]
    public class ImportOptions
    {
        [Option('d', "directory", HelpText = "Path to the new repository")]
        public string Path { get; set; } = Directory.GetCurrentDirectory();
    }

    [Verb("analyze", HelpText = "Analyze log files in repository")]
    public class AnalyzeOptions
    {
        [Option('d', "directory", HelpText = "Path to the new repository")]
        public string Path { get; set; } = Directory.GetCurrentDirectory();

        [Option('p', "file-pattern", Required = false, HelpText = "Regular Expression to match files")]
        public string FilePattern { get; set; } = null;

        [Option("force", Default = false, HelpText = "Enforce (re)analyze all files")]
        public bool Force { get; set; }
    }

    [Verb("statistic", HelpText = "Generate statistics from the repository")]
    public class StatisticOptions
    {
        [Option('d', "directory", HelpText = "Path to the new repository")]
        public string Path { get; set; } = Directory.GetCurrentDirectory();

        [Option('p', "file-pattern", Required = true, HelpText = "Regular Expression to match files")]
        public string FilePattern { get; set; }
    }

}
