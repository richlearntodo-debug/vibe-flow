using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Records an unhandled exception to a file, and reports what previous runs recorded.
//
// This exists because the dark theme shipped unable to start: an unhandled exception in the host's own
// constructor left no trace on the machine beyond a Windows Error Reporting entry, and the only way to find
// it was to read the .NET Runtime event log. A crash that leaves nothing behind is a crash that ships.
//
// The report deliberately carries the rendering environment (interface fonts, screen size, display scaling,
// theme) beside the exception: those are the facts a report from another machine needs, and they are exactly
// what a user cannot be asked to look up.
internal static class CrashReports
{
    internal const string DirectoryName = "crashes";
    // Oldest reports are pruned: this is a diagnostic trail, not an archive, and an unbounded one would grow
    // without anyone noticing.
    internal const int KeepNewest = 5;

    internal static string DefaultDirectory
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Vibe Flow Remote", "UserData", DirectoryName);
        }
    }

    // Writes one report and returns its path, or an empty string when nothing could be written — a crash
    // handler that throws is worse than useless, so every failure here is swallowed.
    internal static string Write(string source, Exception error)
    {
        return Write(DefaultDirectory, source, error);
    }

    internal static string Write(string directory, string source, Exception error)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".log");
            var report = new StringBuilder();
            report.AppendLine("Vibe Flow crash report");
            report.AppendLine("Recorded: " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            report.AppendLine("Source: " + (source ?? "(unknown)"));
            report.AppendLine("App: " + Application.ProductVersion);
            // Environment.OSVersion reports 6.2 on Windows 10 and 11 for a process without a manifest, which
            // is exactly the fact a report needs; the product name and display version come from the registry
            // instead, so "which Windows is this" is answered rather than guessed.
            report.AppendLine("Windows: " + OperatingSystemDescription.Describe());
            report.AppendLine("Windows (reported): " + Environment.OSVersion.VersionString);
            report.AppendLine("Culture: " + CultureInfo.CurrentCulture.Name + " / UI " + CultureInfo.CurrentUICulture.Name);
            report.AppendLine("64-bit process: " + Environment.Is64BitProcess);
            report.AppendLine("Interface: " + UiFonts.Describe());
            report.AppendLine("Display: " + DisplayEnvironment.Describe());
            AppendException(report, error, 0);
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            PruneOldReports(directory);
            return path;
        }
        catch
        {
            return "";
        }
    }

    private static void AppendException(StringBuilder report, Exception error, int depth)
    {
        if (error == null)
        {
            report.AppendLine("Exception: (none supplied)");
            return;
        }
        string indent = new string(' ', depth * 2);
        report.AppendLine(indent + "Exception: " + error.GetType().FullName);
        report.AppendLine(indent + "Message: " + error.Message);
        report.AppendLine(indent + "Stack:");
        report.AppendLine(error.StackTrace ?? "(no stack trace)");
        // Inner exceptions carry the actual cause often enough that losing them would make the report
        // useless, and a bounded depth keeps a pathological chain from filling the file.
        if (error.InnerException != null && depth < 5)
        {
            report.AppendLine(indent + "Inner:");
            AppendException(report, error.InnerException, depth + 1);
        }
    }

    private static void PruneOldReports(string directory)
    {
        try
        {
            var found = new List<FileInfo>(new DirectoryInfo(directory).GetFiles("crash-*.log"));
            if (found.Count <= KeepNewest) return;
            found.Sort(delegate(FileInfo left, FileInfo right) { return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc); });
            for (int index = KeepNewest; index < found.Count; index++)
            {
                try { found[index].Delete(); } catch { }
            }
        }
        catch { }
    }

    // Reports left by earlier runs, newest first. Used at startup — so the log of a session begins by saying
    // whether the previous one died — and by the exported diagnostics.
    internal static List<string> ExistingReports()
    {
        return ExistingReports(DefaultDirectory);
    }

    internal static List<string> ExistingReports(string directory)
    {
        var paths = new List<string>();
        try
        {
            if (!Directory.Exists(directory)) return paths;
            var found = new List<FileInfo>(new DirectoryInfo(directory).GetFiles("crash-*.log"));
            found.Sort(delegate(FileInfo left, FileInfo right) { return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc); });
            foreach (FileInfo file in found) paths.Add(file.FullName);
        }
        catch { }
        return paths;
    }

    // The first lines of a report, for the log and the exported diagnostics.
    internal static string Summarize(string path, int maximumLines)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            var lines = new List<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (lines.Count >= maximumLines) break;
                lines.Add(line);
            }
            return string.Join(Environment.NewLine, lines.ToArray());
        }
        catch { return ""; }
    }
}

// The Windows edition and version, as a user would recognise them.
internal static class OperatingSystemDescription
{
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyEx(IntPtr key, string subKey, int options, int rights, out IntPtr result);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegQueryValueEx(IntPtr key, string valueName, IntPtr reserved, out int type,
        byte[] data, ref int size);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr key);

    private const int HkeyLocalMachine = unchecked((int)0x80000002);
    private const int KeyRead = 0x20019;

    internal static string Describe()
    {
        // ProductName is not authoritative on its own: a Windows 11 machine can still report "Windows 10 Pro"
        // here (measured on this development machine: ProductName "Windows 10 Pro", DisplayVersion 25H2,
        // build 26200 — Windows 11). The display version and build number are what distinguish the releases,
        // so all three are reported and the reader is not left with a single misleading string.
        string product = ReadValue("ProductName");
        string display = ReadValue("DisplayVersion");
        string build = ReadValue("CurrentBuildNumber");
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(product)) parts.Add(product);
        if (!string.IsNullOrWhiteSpace(display)) parts.Add(display);
        if (!string.IsNullOrWhiteSpace(build)) parts.Add("build " + build);
        return parts.Count > 0
            ? string.Join(" ", parts.ToArray())
            : "(Windows version unavailable: " + Environment.OSVersion.VersionString + ")";
    }

    private static string ReadValue(string name)
    {
        IntPtr key = IntPtr.Zero;
        try
        {
            if (RegOpenKeyEx(new IntPtr(HkeyLocalMachine),
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", 0, KeyRead, out key) != 0) return "";
            int size = 0;
            int type = 0;
            if (RegQueryValueEx(key, name, IntPtr.Zero, out type, null, ref size) != 0 || size <= 0) return "";
            var data = new byte[size];
            if (RegQueryValueEx(key, name, IntPtr.Zero, out type, data, ref size) != 0) return "";
            return Encoding.Unicode.GetString(data, 0, size).TrimEnd('\0', ' ');
        }
        catch { return ""; }
        finally { if (key != IntPtr.Zero) { try { RegCloseKey(key); } catch { } } }
    }
}

// The display facts a report needs, in a form that does not require a window.
//
// The host's own diagnostic reports these beside its window and containers, but a crash can happen before a
// window exists — which is exactly what the dark theme did — so the same facts have to be reachable without
// one. This is the part that does not need the form.
internal static class DisplayEnvironment
{
    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    internal static string Describe()
    {
        try
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            uint dpi = 0;
            uint monitorDpi = 0;
            uint monitorDpiY = 0;
            var origin = new POINT();
            origin.X = bounds.Left + 1;
            origin.Y = bounds.Top + 1;
            // MDT_EFFECTIVE_DPI = 0; the primary monitor's effective DPI is what the interface is scaled by.
            if (GetDpiForMonitor(MonitorFromPoint(origin, 2), 0, out monitorDpi, out monitorDpiY) == 0 &&
                monitorDpi >= 48 && monitorDpi <= 480)
            {
                dpi = monitorDpi;
            }
            if (dpi == 0) dpi = GetDpiForSystem();
            return "screen=" + bounds.Width + "x" + bounds.Height +
                " workarea=" + work.Width + "x" + work.Height +
                " dpi=" + dpi + " scale=" + Math.Round(dpi / 96.0, 2).ToString("0.00", CultureInfo.InvariantCulture) +
                " monitors=" + Screen.AllScreens.Length;
        }
        catch { return "(display unavailable)"; }
    }
}
