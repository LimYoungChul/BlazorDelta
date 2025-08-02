using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorDelta.Core.Helpers
{
    internal static class Extensions
    {
        internal static string GetValueOrDefault(this Dictionary<string, string> dict, string key)
        {
            if (dict.TryGetValue(key, out var value)) 
            { 
                return value; 
            }
            return string.Empty;
        }

        internal static T GetValueOrDefault<K, T>(this Dictionary<K, T> dict, K key, T defautValue) where K : notnull
        {
            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }
            return defautValue;
        }
    }
}
