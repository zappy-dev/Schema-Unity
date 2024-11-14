#define SCHEMA_DEV
using System;
using UnityEngine;
using ILogger = Schema.Core.ILogger;
using Logger = Schema.Core.Logger;

namespace Schema.Unity.Editor
{
    public class UnityLogger : ILogger
    {
        public Logger.LogLevel LogLevel { get; set; } =
#if SCHEMA_DEV
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

            switch (logLevel)
            {
                case Logger.LogLevel.VERBOSE:
                case Logger.LogLevel.INFO:
                    Debug.Log(message);
                    break;
                case Logger.LogLevel.WARNING:
                    Debug.LogWarning(message);
                    break;
                case Logger.LogLevel.ERROR:
                    Debug.LogError(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }
}