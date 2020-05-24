using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace logsplit
{
    public interface ILogInfo
    {
         Regex LineRegex { get; }
         Regex FilenameRegex { get; }
         string TimestampFormat { get; }
         bool TimestampFixOffsetColon { get; }
         string ClientIpName { get; }
         string ClientUserName { get; }
         string TimestampName { get; }
         string RequestMethodName { get; }
         string RequestUriName { get; }
         string ProtocolName { get; }
         string InvalidRequestName { get; }
         string StatusCodeName { get; }
         string BytesSentName { get; }
         string RefererName { get; }
         string UserAgentName { get; }
         List<string> SelfHosts { get; }
    }
}