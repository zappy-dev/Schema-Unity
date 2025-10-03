using System;
using Schema.Core.Logging;

namespace Schema.Core
{
    public class ConsoleLogger : ILogger
    {
        public Logger.LogLevel LogLevel { get; set; } = Logger.LogLevel.VERBOSE;
        public void Log(Logger.LogLevel logLevel, string message)
        {
            if (logLevel < LogLevel) return;

            switch (logLevel)
            {
                case Logger.LogLevel.ERROR:
                    Console.Error.WriteLine(message);
                    break;
                default:
                    Console.Out.WriteLine(message);
                    break;
            }
        }
    }
}