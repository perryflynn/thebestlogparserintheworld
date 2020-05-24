using System;
using System.IO;

namespace logsplit
{
    public class StreamInfo<TStreamWriter> : IDisposable
        where TStreamWriter : StreamWriter
    {
        public string Path { get; set; }
        public string HostName { get; set; }
        public string LogGroup { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
        public TStreamWriter StreamWriter { get; set; }
        public int WriteCounter { get; set; } = 0;

        public string FullFileName => System.IO.Path.Combine(this.Path, this.ToString());
        public string FullFileNameGz => $"{this.FullFileName}.gz";
        public string TemporaryFullFileNameGz => $"{this.FullFileName}.gz.new";

        public void Dispose()
        {
            if (this.StreamWriter != null)
            {
                this.StreamWriter.Dispose();
                this.StreamWriter = null;
            }
        }

        public override string ToString()
        {
            return $"{this.HostName.Replace("-", "_")}-{this.LogGroup.Replace("-", "_")}-{this.Year:0000}-{this.Month:00}.log";
        }

        public bool Equals(string hostName, string logGroup, int year, int month)
        {
            return this.HostName == hostName &&
                this.LogGroup == logGroup &&
                this.Year == year &&
                this.Month == month;
        }

        public bool Equals(StreamInfo<TStreamWriter> info)
        {
            return this.Equals(info.HostName, info.LogGroup, info.Year, info.Month);
        }

        public bool Equals(LogInfo info)
        {
            return this.Equals(info.CollectionHostName, info.CollectionGroupName, info.CollectionYear, info.CollectionMonth);
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is StreamInfo<TStreamWriter> otherInfo)
            {
                return this.Equals(otherInfo.HostName, otherInfo.LogGroup, otherInfo.Year, otherInfo.Month);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(this.HostName, this.LogGroup, this.Year, this.Month).GetHashCode();
        }
    }
}
