using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

        public static Stream OpenFile(this string filePath)
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

            throw new FileNotFoundException($"File '{filePath}' does not exists");
        }

        public static long GetRealSize(this string file)
        {
            if (file.EndsWith(".gz", true, CultureInfo.InvariantCulture))
            {
                try {
                    // get real size from a gzipped file
                    using(var fs = File.OpenRead(file))
                    {
                        fs.Position = fs.Length - 4;
                        var b = new byte[4];
                        fs.Read(b, 0, 4);
                        return BitConverter.ToUInt32(b, 0);
                    }
                }
                catch
                {
                    return 0;
                }
            }
            else
            {
                // get size of a normal file
                return (new FileInfo(file)).Length;
            }
        }
    }
}
