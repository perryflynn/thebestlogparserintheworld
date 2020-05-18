using System.IO;
using System.Linq;

namespace logsplit.Extensions
{
    public static class StringExtensions
    {
        public static bool Exists(this string filePath)
        {
            return File.Exists(filePath) || Directory.Exists(filePath);
        }

        public static bool IsDirectory(this string filePath)
        {
            FileAttributes attr = File.GetAttributes(filePath);
            return attr.HasFlag(FileAttributes.Directory);
        }

        public static bool IsEmptyDirectory(this string filePath, params string[] exclude)
        {
            return Directory.EnumerateFileSystemEntries(filePath, "*.*")
                .Where(f => !exclude.Contains(Path.GetFileName(f)))
                .FirstOrDefault() == null;
        }
    }
}
