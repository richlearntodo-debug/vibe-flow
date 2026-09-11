using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Store (UWP and other packaged) applications are launched through their
// AppUserModelID, not through an executable path, and that id is not the process name:
// the AppUserModelID of Store Notepad is
// Microsoft.WindowsNotepad_11.2604.5.0_x64__8wekyb3d8bbwe!App while the process is
// Notepad.exe. Storing the id as if it were a process name made a packaged application
// invisible to Process.GetProcessesByName, so it was started again on every summon and
// could never be attached to or learned.
//
// The real name is read back from a running process with GetApplicationUserModelId,
// which is the only public way to ask the operating system "which package is this
// process". Nothing here reads a window title, a document or any user content.
internal static class PackagedAppIdentity
{
    internal const string AppsFolderPrefix = "shell:AppsFolder\\";
    // Documented maximum for an AppUserModelID, including the terminating null.
    private const int MaxApplicationUserModelId = 130;
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr processHandle,
        ref int applicationUserModelIdLength, StringBuilder applicationUserModelId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    // A favourite whose executable path is a shell parsing path is a packaged
    // application; it exists in the shell namespace rather than on disk, which is why
    // the previous File.Exists guard silently refused to start it.
    internal static bool IsStoreLaunchTarget(string launchTarget)
    {
        return (launchTarget ?? "").Trim().StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static string AumidFromLaunchTarget(string launchTarget)
    {
        string trimmed = (launchTarget ?? "").Trim();
        if (!trimmed.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)) return "";
        return trimmed.Substring(AppsFolderPrefix.Length).Trim();
    }

    // The AppUserModelID of the process that owns this handle, or "" when the process
    // is not packaged (GetApplicationUserModelId then reports no application).
    private static string ReadAumid(int processId)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (handle == IntPtr.Zero) return "";
            var buffer = new StringBuilder(MaxApplicationUserModelId);
            int length = MaxApplicationUserModelId;
            int result = GetApplicationUserModelId(handle, ref length, buffer);
            if (result != 0 && result != ErrorInsufficientBuffer) return "";
            return buffer.ToString().Trim();
        }
        catch
        {
            return "";
        }
        finally
        {
            if (handle != IntPtr.Zero) CloseHandle(handle);
        }
    }

    // Every running packaged process, keyed by AppUserModelID. One pass is enough to
    // answer both questions the catalogue asks: which ids are running, and what each
    // of them is called as a process.
    internal static IDictionary<string, string> RunningPackagedProcesses()
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return found; }
        foreach (Process process in processes)
        {
            try
            {
                string aumid = ReadAumid(process.Id);
                if (aumid.Length == 0) continue;
                string processName = FocusTargetDescriptor.NormalizeProcessName(process.ProcessName);
                if (processName.Length == 0) continue;
                if (!found.ContainsKey(aumid)) found[aumid] = processName;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return found;
    }

    // Resolves the process name of a packaged application that is running now, matching
    // on the full AppUserModelID first and then on its package part, because the
    // application id after "!" is not always the entry point that got started.
    internal static string ResolveProcessName(string aumid)
    {
        string wanted = (aumid ?? "").Trim();
        if (wanted.Length == 0) return "";
        IDictionary<string, string> running = RunningPackagedProcesses();
        string exact;
        if (running.TryGetValue(wanted, out exact)) return exact;
        string package = PackagePartOf(wanted);
        if (package.Length == 0) return "";
        foreach (KeyValuePair<string, string> entry in running)
        {
            if (string.Equals(PackagePartOf(entry.Key), package, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        return "";
    }

    // Polls until the just-launched packaged application reports its process, so a cold
    // start can be attached to the real process instead of a fabricated name.
    internal static string WaitForProcessName(string aumid, int timeoutMs)
    {
        var timer = Stopwatch.StartNew();
        do
        {
            string resolved = ResolveProcessName(aumid);
            if (resolved.Length > 0) return resolved;
            System.Threading.Thread.Sleep(250);
        }
        while (timer.ElapsedMilliseconds < timeoutMs);
        return "";
    }

    private static string PackagePartOf(string aumid)
    {
        string trimmed = (aumid ?? "").Trim();
        int bang = trimmed.IndexOf('!');
        return bang > 0 ? trimmed.Substring(0, bang) : trimmed;
    }
}
