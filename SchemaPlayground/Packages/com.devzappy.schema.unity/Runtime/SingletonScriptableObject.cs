using UnityEditor;

namespace Schema.Runtime
{
    using UnityEngine;

    public abstract class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Find the asset in the project.
                    // This assumes you have a single instance of this ScriptableObject asset.
                    _instance = Resources.Load<T>(typeof(T).Name); 

                    // If not found in Resources, try to find it in the project (editor only).
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = AssetDatabase.LoadAssetAtPath<T>(path);
                        }
                    }
#endif

                    if (_instance == null)
                    {
                        Debug.LogError($"SingletonScriptableObject<{typeof(T).Name}>: Instance not found. Please create one in the project.");
                    }
                }
                return _instance;
            }
        }
    }
}