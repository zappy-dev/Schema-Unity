using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Schema.Unity.Editor
{
    [InitializeOnLoad]
    public static class EditorMainThread
    {
        public static Task Switch(CancellationToken ct = default)
        {
            // If already on main thread during an editor update callback, return completed
            if (IsMainThread) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Pump()
            {
                EditorApplication.update -= Pump;
                tcs.TrySetResult(true);
            }

            // If canceled before we get a tick, try cancel
            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    EditorApplication.update -= Pump;
                    tcs.TrySetCanceled(ct);
                });
            }

            EditorApplication.update += Pump;
            return tcs.Task;
        }

        // Cheap heuristic: in editor, update/OnGUI callbacks are the main thread context.
        public static bool IsMainThread => true; // Unity editor code runs on the main thread
    }
}