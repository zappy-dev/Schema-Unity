using UnityEditor;
using Logger = Schema.Core.Logger;

namespace Schema.Unity.Editor
{
    [InitializeOnLoad]
    public static class SchemaEditorContext
    {
        static SchemaEditorContext()
        {
            Logger.SetLogger(new UnityLogger());
        }
    }
}