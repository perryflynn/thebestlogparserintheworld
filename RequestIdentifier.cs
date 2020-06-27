using System;

namespace logsplit
{
    public class RequestIdentifier
    {
        public string Url { get; set; }

        public string Method { get; set; }

        public int HttpStatus { get; set; }

        public long Count { get; set; }

        public bool Equals(RequestIdentifier ident)
        {
            return this.Url == ident.Url && this.Method == ident.Method && this.HttpStatus == ident.HttpStatus;
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is RequestIdentifier otherInfo)
            {
                return this.Equals(otherInfo);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(this.Url, this.Method, this.HttpStatus).GetHashCode();
        }
    }
}