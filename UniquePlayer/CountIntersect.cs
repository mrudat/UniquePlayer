using System.Collections.Generic;
using System.Linq;

namespace UniquePlayer
{
    static class CountIntersectExtension
    {
        /// <returns>setA.Intersect(setB).Count</returns>
        public static int CountIntersect<T>(this ISet<T> setA, ISet<T> setB)
        {
            if (setA.Count > setB.Count)
                return setB.CountIntersect(setA);
            return setA.Count(setB.Contains);
        }
    }
}
