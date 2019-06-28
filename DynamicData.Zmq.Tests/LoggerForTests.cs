using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Tests
{
    public class LoggerForTests : ILogger
    {
        public static readonly ILogger Default = new LoggerForTests();

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (null != exception) Console.WriteLine(exception.Message);
            else
            {
                Console.WriteLine(state);
            }

        }
    }
}
