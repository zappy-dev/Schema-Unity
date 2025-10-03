using UnityEditor;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public static class SchemaEditorContext
    {
        
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            var unityLogger = new UnityLogger();
            Logger.SetLogger(unityLogger);
        }
    }
}