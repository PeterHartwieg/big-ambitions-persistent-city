using System;

namespace PersistentCity.Core
{
    public static class BodyHierarchy
    {
        public static int Capacity(int rootCount, int bodyCount, int bodiesStillToGrow)
        {
            if (rootCount < 0 || bodyCount < 1 || bodiesStillToGrow < 0) throw new ArgumentOutOfRangeException(nameof(bodyCount));
            long wanted = rootCount + (long)bodyCount * bodiesStillToGrow;
            return wanted > int.MaxValue ? int.MaxValue : (int)wanted;
        }
    }
}
