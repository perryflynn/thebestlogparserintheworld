using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using logsplit.Extensions;
using System.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Globalization;

namespace logsplit
{
    public class Timeslot
    {
        public DateTime Time { get; set; }

        public long Hits { get; set; }

        [JsonIgnore]
        public Dictionary<string, long> Visitors { get; set; } = new Dictionary<string, long>();

        [JsonIgnore]
        public Dictionary<string, long> Referer { get; set; } = new Dictionary<string, long>();

        [JsonIgnore]
        public Dictionary<string, long> Requests { get; set; } = new Dictionary<string, long>();

        public long VisitorsCount { get; set; }

        public double VisitorHitAvg { get; set; }

        public long VisitorHitMin { get; set; }

        public long VisitorHitMax { get; set; }

        public long Bytes { get; set; }

        public List<Counter> RefererList { get; set; } = new List<Counter>();

        public List<Counter> RequestList { get; set; } = new List<Counter>();

        public Dictionary<int, long> StatusCodes { get; set; } = new Dictionary<int, long>();

        public Dictionary<string, long> Methods { get; set; } = new Dictionary<string, long>();

        [JsonConverter(typeof(EnumLongDictionaryConverter<AddressFamily>))]
        public Dictionary<AddressFamily, long> Families { get; set; } = new Dictionary<AddressFamily, long>();

        [JsonConverter(typeof(EnumLongDictionaryConverter<HttpVersion>))]
        public Dictionary<HttpVersion, long> Protocols { get; set; } = new Dictionary<HttpVersion, long>();

        public List<string> InvalidLines { get; set; } = new List<string>();

        public void ParseHit(ILogInfo info, Match match, string line)
        {
            if (match.Success == true && match.Groups[info.InvalidRequestName].Success == false)
            {
                var ip = match.Groups[info.ClientIpName].Value;
                var family = ip.Contains(":") ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
                var bytes = match.Groups[info.BytesSentName].ToLong();
                var status = match.Groups[info.StatusCodeName].ToInt();
                var method = match.Groups[info.RequestMethodName].Value;
                var referer = match.Groups[info.RefererName].Value;

                // request
                var request = match.Groups[info.RequestUriName].Value;

                if (string.IsNullOrWhiteSpace(request))
                {
                    request = "";
                }
                else if(request.Contains("?"))
                {
                    request = request.Substring(0, request.IndexOf("?"));
                }

                // http version
                HttpVersion httpVersion = HttpVersion.INVALID;
                var protocol = match.Groups[info.ProtocolName].Value;

                if (protocol == "HTTP/0.9") httpVersion = HttpVersion.HTTP09;
                else if (protocol == "HTTP/1.0") httpVersion = HttpVersion.HTTP10;
                else if (protocol == "HTTP/1.1") httpVersion = HttpVersion.HTTP11;
                else if (protocol == "HTTP/2.0") httpVersion = HttpVersion.HTTP20;
                else if (protocol == "HTTP/3.0") httpVersion = HttpVersion.HTTP30;

                // referer sets
                if (string.IsNullOrWhiteSpace(referer) || referer == "-")
                {
                    referer = "SET_EMPTY";
                }
                else if (referer.EndsWith("/device-map/ssinfo.asp"))
                {
                    referer = "SET_DEVICEMAP_SSINFO";
                }
                else
                {
                    try
                    {
                        var refererUri = new Uri(referer);
                        var host = refererUri.Host;

                        if (info.SelfHosts.Contains(host))
                        {
                            referer = "SET_SELF";
                        }
                        else if (refererUri.HostNameType == UriHostNameType.IPv4)
                        {
                            var parts = host.Split('.').Select(p => int.Parse(p)).ToArray();

                            if (host.StartsWith("10.") ||
                                (parts[0] == 100 && parts[1] >= 64 && parts[1] <= 127) ||
                                host.StartsWith("127.") ||
                                host.StartsWith("169.254.") ||
                                (parts[0] == 172 && parts[1] >= 16 && parts[1] <= 31) ||
                                host.StartsWith("192.0.0.") || host.StartsWith("192.0.2.") ||
                                host.StartsWith("192.168.") ||
                                host.StartsWith("198.18.") || host.StartsWith("198.19.") ||
                                (parts[0] >= 224 && parts[0] <= 239))
                            {
                                referer = "SET_PRIVATE_IPv4";
                            }
                        }
                        else if (refererUri.HostNameType == UriHostNameType.IPv6)
                        {
                            var first4ok = int.TryParse(host.Substring(0,4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int first4);

                            if(host.StartsWith("::") ||
                                (first4ok && first4 >= 65152 && first4 <= 65215))
                            {
                                referer = "SET_PRIVATE_IPv6";
                            }
                        }
                        else if (refererUri.HostNameType == UriHostNameType.Dns && !host.Contains("."))
                        {
                            referer = "SET_PRIVATE_HOSTNAME";
                        }
                    }
                    catch
                    {
                        referer = "SET_INVALID";
                    }
                }

                this.Hits++;
                this.Bytes += bytes;

                this.Visitors.EnsureField(ip, () => 0, dict => dict[ip]++);
                this.StatusCodes.EnsureField(status, () => 0, dict => dict[status]++);
                this.Methods.EnsureField(method, () => 0, dict => dict[method]++);
                this.Families.EnsureField(family, () => 0, dict => dict[family]++);
                this.Protocols.EnsureField(httpVersion, () => 0, dict => dict[httpVersion]++);
                this.Referer.EnsureField(referer, () => 0, dict => dict[referer]++);
                this.Requests.EnsureField(request, () => 0, dict => dict[request]++);
            }
            else
            {
                this.InvalidLines.Add(line);
            }
        }

        [OnSerializing]
        internal void OnSerialize(StreamingContext context)
        {
            this.VisitorsCount = this.Visitors.Count;
            this.VisitorHitAvg = this.Visitors.Values.Average();
            this.VisitorHitMin = this.Visitors.Values.Min();
            this.VisitorHitMax = this.Visitors.Values.Max();

            this.RefererList = this.Referer
                .Select(kv => new Counter() { Value = kv.Key, Count = kv.Value })
                .OrderBy(kv => kv.Value)
                .ToList();

            this.RequestList = this.Requests
                .Select(kv => new Counter() { Value = kv.Key, Count = kv.Value })
                .OrderBy(kv => kv.Value)
                .ToList();
        }

        public static string FixTimestampOffset(ILogInfo info, string timestamp)
        {
            var begin = timestamp.Substring(0, timestamp.Length - 2);
            var end = timestamp.Substring(timestamp.Length - 2);
            return $"{begin}:{end}";
        }

        public static bool TryParseTimestamp(ILogInfo info, string timestamp, out DateTime parsedTimestamp)
        {
            var fixedTs = timestamp;
            if (info.TimestampFixOffsetColon)
            {
                fixedTs = FixTimestampOffset(info, fixedTs);
            }

            try
            {
                parsedTimestamp = DateTime.ParseExact(fixedTs, info.TimestampFormat, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                parsedTimestamp = DateTime.MinValue;
                return false;
            }
        }
    }
}
