using System;
using System.Collections.Generic;
using System.Text;

namespace Inference.Common
{
    public static class Hashing
    {
        public const int Start = unchecked((int)2166136261);

        public static int Hash<T>(this int hash, T obj)
        {
            var h = EqualityComparer<T>.Default.GetHashCode(obj);
            return unchecked((hash * 16777619) ^ h);
        }
    }
}
