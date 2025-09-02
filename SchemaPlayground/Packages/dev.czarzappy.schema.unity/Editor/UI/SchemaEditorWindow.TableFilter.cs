using System;
using UnityEditor;
using System.Collections.Generic;
using Schema.Core.Data;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        // Add this field to store filter values for each attribute
        private Dictionary<string, string> attributeFilters = new Dictionary<string, string>();
        private event Action OnAttributeFiltersUpdated;

        private string GetFilterPrefsKey(string schemeName) => $"SchemaEditorWindow.AttributeFilters.{schemeName}";

        private void SaveAttributeFilters(string schemeName)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(attributeFilters);
            EditorPrefs.SetString(GetFilterPrefsKey(schemeName), json);
        }

        private void LoadAttributeFilters(string schemeName)
        {
            var key = GetFilterPrefsKey(schemeName);
            if (EditorPrefs.HasKey(key))
            {
                var json = EditorPrefs.GetString(key);
                attributeFilters = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                                  ?? new Dictionary<string, string>();
            }
            else
            {
                attributeFilters = new Dictionary<string, string>();
            }
        }

        private void UpdateAttributeFilter(string attributeName, string newFilterValue)
        {
            if (string.IsNullOrWhiteSpace(newFilterValue))
            {
                attributeFilters.Remove(attributeName);
            }
            else
            {
                attributeFilters[attributeName] = newFilterValue;
            }

            OnAttributeFiltersUpdated?.Invoke();
        }

        private List<(AttributeDefinition attributeDefinition, string needle)> GetAttributeFiltersForScheme(DataScheme scheme)
        {
            var compiledFilters = new List<(AttributeDefinition attribute, string needle)>();
            foreach (var kvp in attributeFilters)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                    continue;
                            
                var attributeRes = scheme.GetAttribute(kvp.Key);
                if (!attributeRes.Try(out var attribute))
                    continue;

                var sanitizedNeedle = kvp.Value.Trim().ToLower();
                compiledFilters.Add((attribute, sanitizedNeedle));
            }

            return compiledFilters;
        }
    }
}