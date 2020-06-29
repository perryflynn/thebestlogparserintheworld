using System;
using logsplit.Tasks;

namespace logsplit
{
    public class StreamInfo : IGZipWriterKey
    {
        public string Directory { get; set; }
        public string HostName { get; set; }
        public string LogGroup { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;

        public string FullFileName => System.IO.Path.Combine(this.Directory, this.ToString());
        public string FullFileNameGz => $"{this.FullFileName}.gz";
        public string TemporaryFullFileNameGz => $"{this.FullFileName}.gz.new";

        public StreamInfo()
        {
        }

        public StreamInfo(string directory, ILogInfo logInfo)
        {
            this.Directory = directory;
            this.HostName = logInfo.CollectionHostName;
            this.LogGroup = logInfo.CollectionGroupName;
            this.Year = logInfo.CollectionYear;
            this.Month = logInfo.CollectionMonth;
        }

        public string GetGZipWriterFileName()
        {
            return this.FullFileNameGz;
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

        public bool Equals(StreamInfo info)
        {
            return this.Equals(info.HostName, info.LogGroup, info.Year, info.Month);
        }

        public bool Equals(ILogInfo info)
        {
            return this.Equals(info.CollectionHostName, info.CollectionGroupName, info.CollectionYear, info.CollectionMonth);
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is StreamInfo otherInfo)
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
