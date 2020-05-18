using System;
using System.Collections.Generic;

namespace logsplit.Extensions
{
    public static class DictionaryExtensions
    {
        public static void EnsureField<T1, T2>(this Dictionary<T1, T2> dictionary, T1 key, Func<T2> defaultValue, Action<Dictionary<T1, T2>> func)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, defaultValue());
            }

            func(dictionary);
        }
    }
}
