using System;
using System.IO;
using System.Reflection;
using HBS.Logging;
using vfBattleTechMod_Core.Utils.Interfaces;
using LogLevel = vfBattleTechMod_Core.Utils.Enums.LogLevel;

namespace vfBattleTechMod_Core.Utils.Loggers
{
    public class HbsLogger : ILogger
    {
        private readonly ILogAppender logAppender;

        private readonly ILog logger;
        private LogLevel _logLevel;

        public LogLevel LogLevel
        {
            get => _logLevel;
            set
            {
                _logLevel = value;
            }
        }

        public HbsLogger(ILog logger, string directory, string moduleName)
        {
            var logFileName = $"{moduleName}-log.txt";
            var logFilePath = Path.Combine(directory, logFileName);

            if (File.Exists(logFilePath))
            {
                try
                {
                    File.Delete(logFilePath);
                }
                catch (Exception)
                {
                    logger.LogDebug($"Failed to delete existing log file [{logFilePath}]");
                }
            }

            this.logger = logger;
            logAppender = new FileLogAppender(
                logFilePath,
                FileLogAppender.WriteMode.INSTANT);
            Logger.AddAppender(moduleName, logAppender);
        }

        public void Debug(string message)
        {
            if (LogLevel >= LogLevel.Debug)
            {
                logger.Log(message);
            }
        }

        public void Error(string message, Exception ex)
        {
            logger.LogError(message);
        }

        public void Trace(string message)
        {
            if (LogLevel >= LogLevel.Trace)
            {
                logger.LogDebug(message);
            }
        }
    }
    
    public class NonSillyLogging
    {
        private static LogLevel _logLevel;
        public static LogLevel LogLevel
        {
            get => _logLevel;
            set
            {
                _logLevel = value;
            }
        }

        internal static string LogFilePath =>
        Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "\\vfCoreLogging.txt";

        public static void Error(Exception ex)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine($"{ex}");
                writer.WriteLine($"Message: {ex.Message}");
                writer.WriteLine($"StackTrace: {ex.StackTrace}");
                writer.WriteLine($"Source: {ex.Source}");
                writer.WriteLine($"Data: {ex.Data}");
            }
        }

        public static void LogDebug(object line)
        {
            if (LogLevel >= LogLevel.Debug) return;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line.ToString());
            }
        }
        public static void Log(string line)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line);
            }
        }
    }
}