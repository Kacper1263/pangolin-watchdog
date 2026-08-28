using System.Diagnostics;
using PangolinWatchdog.Data;

namespace PangolinWatchdog.Helpers;

public static class ReadOnlyMode
{
    public static bool IsEnabled(AppConfig config) => config.ReadOnlyMode || Debugger.IsAttached;
}
