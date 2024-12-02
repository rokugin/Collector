using StardewModdingAPI;

namespace Collector;

static class Log {

    /// <summary>
    /// Log an Info (white) message for the player or developer.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="alwaysShow">Whether to show the message regardless of config settings. Defaults to <c>false</c>.</param>
    public static void Info(string message, bool alwaysShow = false) {
        ModEntry.SMonitor.Log(message, DesiredLogLevel(alwaysShow, LogLevel.Info));
    }

    public static void Debug(string message, bool alwaysShow = false) {
        ModEntry.SMonitor.Log(message, DesiredLogLevel(alwaysShow, LogLevel.Debug));
    }

    static LogLevel DesiredLogLevel(bool alwaysShow, LogLevel logLevel) {
        return alwaysShow ? logLevel : ModEntry.Config.Logging ? logLevel : LogLevel.Trace;
    }

}