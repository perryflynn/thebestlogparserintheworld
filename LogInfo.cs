using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace logsplit
{
    public class LogInfo : ILogInfo
    {
        [JsonIgnore]
        private string rgxstr = @"^(?<ClientIP>[^\s]+)\s+-\s+(?<ClientUser>[^\s]+)\s+\[(?<Timestamp>[^\]]+)\]\s+""(?:(?:(?<RequestMethod>[A-Z]+)\s+(?<RequestUri>[^\s]+)\s+(?<Protocol>[^""]+)|(?<InvalidRequest>.+)))?""\s+(?<StatusCode>[0-9]+)\s+(?<BytesSent>[0-9]+)\s+""(?<Referer>[^""]*)""\s+""(?<UserAgent>[^""]*)""$";

        [JsonIgnore]
        private Regex rgx = new Regex(@"^(?<ClientIP>[^\s]+)\s+-\s+(?<ClientUser>[^\s]+)\s+\[(?<Timestamp>[^\]]+)\]\s+""(?:(?:(?<RequestMethod>[A-Z]+)\s+(?<RequestUri>[^\s]+)\s+(?<Protocol>[^""]+)|(?<InvalidRequest>.+)))?""\s+(?<StatusCode>[0-9]+)\s+(?<BytesSent>[0-9]+)\s+""(?<Referer>[^""]*)""\s+""(?<UserAgent>[^""]*)""$", RegexOptions.Compiled);

        [JsonIgnore]
        private string filergxstr = @"/(?<HostName>[^/]+)/(?<GroupName>[^/-]+)[^/]+\.gz$";

        [JsonIgnore]
        private Regex filergx = new Regex(@"/(?<HostName>[^/]+)/(?<GroupName>[^/-]+)[^/]+\.gz$");

        public string LineRegexStr
        {
            get { return this.rgxstr; }
            set
            {
                this.rgxstr = value;
                this.rgx = new Regex(value, RegexOptions.Compiled);
            }
        }

        public string FileRegexStr
        {
            get { return this.filergxstr; }
            set
            {
                this.filergxstr = value;
                this.filergx = new Regex(value, RegexOptions.Compiled);
            }
        }

        [JsonIgnore]
        public Regex LineRegex => this.rgx;

        [JsonIgnore]
        public Regex FilenameRegex => this.filergx;

        public string TimestampFormat { set; get; } = "dd/MMM/yyyy:HH:mm:ss";

        public bool TimestampFixOffsetColon { get; set; } = true;

        public string CollectionHostName { get; set; }

        public string CollectionGroupName { get; set; }

        public int CollectionYear { get; set; }

        public int CollectionMonth { get; set; }

        public string ClientIpName { set; get; } = "ClientIP";

        public string ClientUserName { set; get; } = "ClientUser";

        public string TimestampName { set; get; } = "Timestamp";

        public string RequestMethodName { set; get; } = "RequestMethod";

        public string RequestUriName { set; get; } = "RequestUri";

        public string ProtocolName { set; get; } = "Protocol";

        public string InvalidRequestName { set; get; } = "InvalidRequest";

        public string StatusCodeName { set; get; } = "StatusCode";

        public string BytesSentName { set; get; } = "BytesSent";

        public string RefererName { set; get; } = "Referer";

        public string UserAgentName { set; get; } = "UserAgent";

        public List<string> SelfHosts { get; set; } = new List<string>();
    }
}
