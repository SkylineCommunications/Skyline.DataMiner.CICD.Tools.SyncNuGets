namespace Skyline.DataMiner.CICD.Tools.SyncNuGets
{
    using System;
    using System.Threading.Tasks;

    using NuGet.Common;

    /// <summary>
    /// Implements ILogger to provide logging functionality.
    /// </summary>
    internal class MyLogger : ILogger
    {
        /// <summary>
        /// Logs a message with a specified log level.
        /// </summary>
        /// <param name="level">The log level of the message.</param>
        /// <param name="data">The message to log.</param>
        public void Log(LogLevel level, string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs a message contained within an ILogMessage instance.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(ILogMessage message)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// Asynchronously logs a message with a specified log level.
        /// </summary>
        /// <param name="level">The log level of the message.</param>
        /// <param name="data">The message to log.</param>
        /// <returns>A task that represents the asynchronous log operation.</returns>
        public Task LogAsync(LogLevel level, string data)
        {
            return Task.Run(() => Console.WriteLine(data));
        }

        /// <summary>
        /// Asynchronously logs a message contained within an ILogMessage instance.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A task that represents the asynchronous log operation.</returns>
        public Task LogAsync(ILogMessage message)
        {
            return Task.Run(() => Console.WriteLine(message));
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="data">The debug message to log.</param>
        public void LogDebug(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="data">The error message to log.</param>
        public void LogError(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="data">The informational message to log.</param>
        public void LogInformation(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs a summary of informational messages.
        /// </summary>
        /// <param name="data">The summary message to log.</param>
        public void LogInformationSummary(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs a minimal message.
        /// </summary>
        /// <param name="data">The minimal message to log.</param>
        public void LogMinimal(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="data">The verbose message to log.</param>
        public void LogVerbose(string data)
        {
            Console.WriteLine(data);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="data">The warning message to log.</param>
        public void LogWarning(string data)
        {
            Console.WriteLine(data);
        }
    }
}