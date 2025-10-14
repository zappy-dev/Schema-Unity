using System;
using System.Threading;
using UnityEngine;
using ILogger = Schema.Core.Logging.ILogger;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public class UnityLogger : ILogger
    {
        private Thread mainThread;
        public UnityLogger()
        {
            mainThread = System.Threading.Thread.CurrentThread;
        }

        private bool IsMainThread => mainThread.Equals(System.Threading.Thread.CurrentThread);
        
        public Logger.LogLevel LogLevel { get; set; } =
#if SCHEMA_DEBUG
            Logger.LogLevel.VERBOSE;
#else
            Logger.LogLevel.INFO;
#endif
        
        public void Log(Logger.LogLevel logLevel, string message)
        {
            if (logLevel < LogLevel)
            {
                return;
            }

            string msg = message;
#if SCHEMA_DEBUG
            if (IsMainThread)
            {
                msg = $"[{Time.frameCount}] {message}";
            }
#endif

            switch (logLevel)
            {
                case Logger.LogLevel.VERBOSE:
                case Logger.LogLevel.INFO:
                    Debug.Log(msg);
                    break;
                case Logger.LogLevel.WARN:
                    Debug.LogWarning(msg);
                    break;
                case Logger.LogLevel.ERROR:
                    Debug.LogError(msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }
}