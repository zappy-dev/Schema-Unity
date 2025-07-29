using UnityEditor;
using System.Collections.Generic;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        // Add this field to store filter values for each attribute
        private Dictionary<string, string> attributeFilters = new Dictionary<string, string>();

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
    }
}