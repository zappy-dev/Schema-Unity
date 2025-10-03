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

        /// <summary>
        /// Builds the EditorPrefs key used to persist attribute filters for a given scheme.
        /// </summary>
        private string GetFilterPrefsKey(string schemeName) => $"SchemaEditorWindow.AttributeFilters.{schemeName}";

        /// <summary>
        /// Persists the current in-memory <see cref="attributeFilters"/> for the provided scheme name.
        /// </summary>
        private void SaveAttributeFilters(string schemeName)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(attributeFilters);
            EditorPrefs.SetString(GetFilterPrefsKey(schemeName), json);
        }

        /// <summary>
        /// Loads attribute filters for the provided scheme name into memory.
        /// This should only be called on scheme selection changes or window initialization,
        /// not during rendering/updates.
        /// </summary>
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

        /// <summary>
        /// Updates the in-memory filter for a specific attribute and saves the entire filter set
        /// for the currently selected scheme before notifying listeners. This ordering ensures that
        /// any subscribers that react immediately (e.g., to refresh table entries) will observe the
        /// updated in-memory state and persisted preferences.
        /// </summary>
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

            // Persist before notifying so consumers reading immediately don't reload stale prefs
            if (!string.IsNullOrEmpty(selectedSchemeName))
            {
                SaveAttributeFilters(selectedSchemeName);
            }

            OnAttributeFiltersUpdated?.Invoke();
        }

        /// <summary>
        /// Compiles the active attribute filters for the provided scheme into an efficient form
        /// for matching. Assumes <see cref="attributeFilters"/> reflects the latest in-memory state;
        /// no preferences are loaded here to avoid timing issues with UI updates.
        /// </summary>
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

                foreach (var value in kvp.Value.Split(','))
                {
                    var sanitizedNeedle = value;
                    compiledFilters.Add((attribute, sanitizedNeedle));
                }
            }

            return compiledFilters;
        }
    }
}