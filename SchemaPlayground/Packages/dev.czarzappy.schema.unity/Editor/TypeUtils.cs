using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class TypeUtils
    {
        public static IEnumerable<Type> GetUserDefinedScriptableObjectTypes()
        {
            var scriptableObjectType = typeof(ScriptableObject);
            var editorWindowType = typeof(EditorWindow);
            var editorType = typeof(UnityEditor.Editor);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("Unity.") && // filter Unity-related assembles.
                            !a.FullName.StartsWith("UnityEngine") &&
                            !a.FullName.StartsWith("UnityEditor") &&
                            !a.FullName.Contains("Unity.Editor") &&
                            !a.FullName.StartsWith("Schema")); // Filter Schema assemblies
            var soTypes = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => scriptableObjectType.IsAssignableFrom(t) && 
                            !(editorWindowType.IsAssignableFrom(t) || editorType.IsAssignableFrom(t))); // Editor and EditorWindow are Scriptable Objects :upsidedown-smile:

            return soTypes;
        }

        public static IEnumerable<FieldInfo> GetSerializedFieldsForType(Type type)
        {
            var serializedFieldAttribute = typeof(SerializeField);
            var serializedFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute(serializedFieldAttribute) != null);

            return serializedFields;
        }
    }
}