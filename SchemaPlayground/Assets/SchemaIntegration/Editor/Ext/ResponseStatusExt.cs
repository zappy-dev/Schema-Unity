using Schema.Core;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity
{
    public static class SchemaResponseExt
    {
        public static MessageType MessageType(this SchemaResponse response)
        {
            switch (response.Status)
            {
                case RequestStatus.Success:
                    return UnityEditor.MessageType.Info;
                case RequestStatus.Error:
                    return UnityEditor.MessageType.Error;
                default:
                    Debug.LogErrorFormat("Schema{0}", response.Status);
                    return UnityEditor.MessageType.None;
            }
        }
    }
}