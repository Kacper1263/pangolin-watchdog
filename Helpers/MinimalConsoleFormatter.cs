using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace PangolinWatchdog.Helpers;

public class MinimalConsoleFormatter : ConsoleFormatter
{
    public MinimalConsoleFormatter() : base("minimal") { }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        var levelString = logEntry.LogLevel switch
        {
            LogLevel.Information => "INFO",
            LogLevel.Warning     => "WARN",
            LogLevel.Error       => "FAIL",
            LogLevel.Critical    => "CRIT",
            LogLevel.Debug       => "DBUG",
            LogLevel.Trace       => "TRCE",
            _                    => "LOG "
        };

        var originalColor = Console.ForegroundColor;
        var levelColor = logEntry.LogLevel switch
        {
            LogLevel.Information => ConsoleColor.Cyan,
            LogLevel.Warning     => ConsoleColor.Yellow,
            LogLevel.Error       => ConsoleColor.Red,
            LogLevel.Critical    => ConsoleColor.DarkRed,
            _                    => ConsoleColor.DarkGray
        };
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        textWriter.Write($"[{timestamp}] ");

        Console.ForegroundColor = levelColor;
        textWriter.Write($"[{levelString}] ");

        Console.ForegroundColor = ConsoleColor.Gray; 
        textWriter.WriteLine(message);

        if (logEntry.Exception != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            textWriter.WriteLine(logEntry.Exception.ToString());
        }

        Console.ForegroundColor = originalColor;
    }
}