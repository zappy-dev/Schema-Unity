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

        /// <summary>
        /// Computes a hash code based on the contents of a list (value-based hashing).
        /// This ensures that two lists with the same elements have the same hash code.
        /// </summary>
        public static int GetListHashCode<T>(List<T> list)
        {
            if (list == null) return 0;
            
            unchecked
            {
                int hash = 17;
                foreach (var item in list)
                {
                    hash = hash * 31 + (item != null ? EqualityComparer<T>.Default.GetHashCode(item) : 0);
                }
                return hash;
            }
        }
    }
}