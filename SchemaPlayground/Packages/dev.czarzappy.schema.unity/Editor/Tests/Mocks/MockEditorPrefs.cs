using System.Collections.Generic;

namespace Schema.Unity.Editor.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of EditorPrefs for testing without affecting real Unity settings
    /// </summary>
    public static class MockEditorPrefs
    {
        private static Dictionary<string, object> prefs = new Dictionary<string, object>();
        
        public static void SetString(string key, string value)
        {
            prefs[key] = value;
        }
        
        public static string GetString(string key, string defaultValue = "")
        {
            return prefs.ContainsKey(key) ? (string)prefs[key] : defaultValue;
        }
        
        public static void SetInt(string key, int value)
        {
            prefs[key] = value;
        }
        
        public static int GetInt(string key, int defaultValue = 0)
        {
            return prefs.ContainsKey(key) ? (int)prefs[key] : defaultValue;
        }
        
        public static void SetBool(string key, bool value)
        {
            prefs[key] = value;
        }
        
        public static bool GetBool(string key, bool defaultValue = false)
        {
            return prefs.ContainsKey(key) ? (bool)prefs[key] : defaultValue;
        }
        
        public static void SetFloat(string key, float value)
        {
            prefs[key] = value;
        }
        
        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return prefs.ContainsKey(key) ? (float)prefs[key] : defaultValue;
        }
        
        public static void Clear()
        {
            prefs.Clear();
        }
        
        public static bool HasKey(string key)
        {
            return prefs.ContainsKey(key);
        }
        
        public static void DeleteKey(string key)
        {
            if (prefs.ContainsKey(key))
            {
                prefs.Remove(key);
            }
        }
    }
}