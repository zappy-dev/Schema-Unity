using Schema.Core;
using UnityEditor;
using UnityEngine;
using static Schema.Core.SchemaResult;

namespace Schema.Unity
{
    public static class SchemaResultExt
    {
        public static MessageType MessageType(this SchemaResult response)
        {
            switch (response.Status)
            {
                case RequestStatus.Passed:
                    return UnityEditor.MessageType.Info;
                case RequestStatus.Failed:
                    return UnityEditor.MessageType.Error;
                default:
                    Debug.LogErrorFormat("Schema{0}", response.Status);
                    return UnityEditor.MessageType.None;
            }
        }
        
        public static MessageType MessageType<T>(this SchemaResult<T> response)
        {
            switch (response.Status)
            {
                case RequestStatus.Passed:
                    return UnityEditor.MessageType.Info;
                case RequestStatus.Failed:
                    return UnityEditor.MessageType.Error;
                default:
                    Debug.LogErrorFormat("Schema{0}", response.Status);
                    return UnityEditor.MessageType.None;
            }
        }
    }
}