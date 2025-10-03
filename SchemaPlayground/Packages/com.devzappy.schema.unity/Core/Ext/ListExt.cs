using System.Collections.Generic;

namespace Schema.Core.Ext
{
    public static class ListExt
    {
        public static bool ListsAreEqual<T>(List<T> list1, List<T> list2)
        {
            if (list1.Count != list2.Count) return false;
            for (int i = 0; i < list1.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(list1[i], list2[i]))
                    return false;
            }
            return true;
        }
    }
}