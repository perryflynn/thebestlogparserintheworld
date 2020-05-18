using System.Text.RegularExpressions;

namespace logsplit.Extensions
{
    public static class GroupExtensions
    {
        public static long ToLong(this Group group)
        {
            return group.Success ? long.Parse(group.Value) : 0;
        }

        public static int ToInt(this Group group)
        {
            return group.Success ? int.Parse(group.Value) : 0;
        }
    }
}