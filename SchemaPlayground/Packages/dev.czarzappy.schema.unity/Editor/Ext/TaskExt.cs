using System;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor.Ext
{
    public static class TaskExt
    {
        /// <summary>
        /// Safely runs a Task in fire-and-forget mode.
        /// Logs exceptions to Unity console.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="onException">Optional handler for exceptions.</param>
        public static async void FireAndForget(this Task task, Action<Exception> onException = null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (onException != null)
                    onException(ex);
                else
                    Logger.LogError(ex.Message);
            }
        }
    }
}