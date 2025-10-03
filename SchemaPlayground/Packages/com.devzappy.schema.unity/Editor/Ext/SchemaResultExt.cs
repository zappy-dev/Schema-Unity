using Schema.Core;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Ext
{
    /// <summary>
    /// Extension methods for converting Schema result statuses to Unity editor message types.
    /// </summary>
    public static class SchemaResultExt
    {
        /// <summary>
        /// Converts a SchemaResult status to a Unity MessageType for editor display.
        /// </summary>
        /// <param name="response">The Schema result to convert.</param>
        /// <returns>A Unity MessageType corresponding to the Schema result status:
        /// Info for Passed, Error for Failed, None for unknown statuses.</returns>
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
        
        /// <summary>
        /// Converts a generic SchemaResult status to a Unity MessageType for editor display.
        /// </summary>
        /// <typeparam name="T">The type of the result payload.</typeparam>
        /// <param name="response">The generic Schema result to convert.</param>
        /// <returns>A Unity MessageType corresponding to the Schema result status:
        /// Info for Passed, Error for Failed, None for unknown statuses.</returns>
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