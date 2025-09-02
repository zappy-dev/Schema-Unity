using System.Globalization;
using Schema.Core.DataStructures;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class SchemaGUI
    {
        private class StringConvertCache
        {
            private string conversationFormat;

            public StringConvertCache()
            {
                
            }
            // Int-to-string cache
            private static readonly LRUCache<int, string> cache = new LRUCache<int, string>(1024);

            public string GetOrAdd(int value)
            {
                if (cache.TryGet(value, out var str))
                {
                    return str;
                }

                str = value.ToString(kIntFieldFormatString, CultureInfo.InvariantCulture);
                cache.Put(value, str);
                return str;
            }
        }

        private static StringConvertCache IntToStringCache = new StringConvertCache();
        
        /// <summary>
        /// A faster implementation of EditorGUI.IntField()
        /// </summary>
        /// <param name="cellRect"></param>
        /// <param name="value"></param>
        /// <param name="cellStyleFieldStyle"></param>
        /// <returns></returns>
        public static int IntField(Rect cellRect, int value, GUIStyle cellStyleFieldStyle)
        {
            // turns out EditorGUI.IntField is really slow under the hood because it does a string coversion under the hood every frame.
            // using var changeCheckScope = new EditorGUI.ChangeCheckScope();
            
            var intVal = IntToStringCache.GetOrAdd(value);
            
            EditorGUI.BeginChangeCheck();
            var newInt = EditorGUI.TextField(cellRect, intVal, cellStyleFieldStyle);
            var changed = EditorGUI.EndChangeCheck();

            // Only do conversion back if field changed
            if (changed)
            {
                // See EditorGUI.cs::DoIntField() and DoNumberField()
                StringToLong(newInt, out var longVal);
                return MathUtils.ClampToInt(longVal);
            }

            return value;
        }
        
        internal static bool StringToLong(string str, out long value)
        {
            return ExpressionEvaluator.Evaluate<long>(str, out value);
        }

        private static string kIntFieldFormatString = "#######0";
    }
}