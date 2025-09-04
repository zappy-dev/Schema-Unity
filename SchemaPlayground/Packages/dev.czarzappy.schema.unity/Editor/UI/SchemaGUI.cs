using System;
using System.Globalization;
using Schema.Core.DataStructures;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class SchemaGUI
    {
        private class StringConvertCache<T>
        {
            private string conversationFormat;

            private Func<T, string> conversionFunc;
            
            public StringConvertCache(Func<T, string> conversionFunc)
            {
                this.conversionFunc = conversionFunc;
            }
            
            // Int-to-string cache
            private static readonly LRUCache<T, string> cache = new LRUCache<T, string>(1024);

            public string GetOrAdd(T value)
            {
                if (cache.TryGet(value, out var str))
                {
                    return str;
                }

                str = conversionFunc(value);
                cache.Put(value, str);
                return str;
            }
        }

        private static StringConvertCache<int> IntToStringCache = new StringConvertCache<int>((value) => value.ToString(kIntFieldFormatString, CultureInfo.InvariantCulture));
        private static StringConvertCache<float> FloatToStringCache = new StringConvertCache<float>((value) => value.ToString(kFloatFieldFormatString, CultureInfo.InvariantCulture));
        
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
            // EditorGUI.IntField()
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
        /// <summary>
        /// A faster implementation of EditorGUI.IntField()
        /// </summary>
        /// <param name="cellRect"></param>
        /// <param name="value"></param>
        /// <param name="cellStyleFieldStyle"></param>
        /// <returns></returns>
        public static float FloatField(Rect cellRect, float value, GUIStyle cellStyleFieldStyle)
        {
            // turns out EditorGUI.IntField is really slow under the hood because it does a string coversion under the hood every frame.
            // using var changeCheckScope = new EditorGUI.ChangeCheckScope();
            
            var intVal = FloatToStringCache.GetOrAdd(value);
            
            EditorGUI.BeginChangeCheck();
            // EditorGUI.FloatField()
            var newInt = EditorGUI.TextField(cellRect, intVal, cellStyleFieldStyle);
            var changed = EditorGUI.EndChangeCheck();

            // Only do conversion back if field changed
            if (changed)
            {
                // See EditorGUI.cs::DoIntField() and DoNumberField()
                StringToFloat(newInt, out var floatVal);
                return floatVal;
            }

            return value;
        }
        
        internal static bool StringToLong(string str, out long value)
        {
            return ExpressionEvaluator.Evaluate(str, out value);
        }
        
        internal static bool StringToFloat(string str, out float value)
        {
            return ExpressionEvaluator.Evaluate(str, out value);
        }

        private static string kIntFieldFormatString = "#######0";
        private static string kFloatFieldFormatString = "g7";
    }
}