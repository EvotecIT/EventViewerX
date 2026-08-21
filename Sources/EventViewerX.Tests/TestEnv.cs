using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;

namespace EventViewerX.Tests;

internal static class TestEnv
{
    internal static bool IsAdmin()
    {
        try
        {
            using WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    internal static bool CanReadLog(string logName)
    {
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName) { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            using var rec = reader.ReadEvent(TimeSpan.FromMilliseconds(250));
            return rec != null;
        }
        catch
        {
            return false;
        }
    }
}
