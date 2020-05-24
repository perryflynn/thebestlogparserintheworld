using System.Collections.Generic;
using System.IO;
using System.Text;

namespace logsplit.Extensions
{
    public static class StreamExtensions
    {
        public static IEnumerable<string> ReadLineToEnd(this Stream stream)
        {
            using(var reader = new StreamReader(stream, Encoding.UTF8, false, -1, false))
            {
                while(reader.Peek() >= 0)
                {
                    yield return reader.ReadLine();
                }
            }
        }
    }
}
