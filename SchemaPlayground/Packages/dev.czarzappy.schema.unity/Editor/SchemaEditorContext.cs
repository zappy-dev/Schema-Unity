using UnityEditor;
using Logger = Schema.Core.Logger;

namespace Schema.Unity.Editor
{
    public static class SchemaEditorContext
    {
        
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            Logger.SetLogger(new UnityLogger());
        }
    }
}