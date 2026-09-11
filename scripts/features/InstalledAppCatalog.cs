using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;

// Inventory of the applications installed on this machine, so a favourite can be added
// before the application has ever been started. Start-menu shortcuts are the reliable
// source: they are what the user actually launches, and their target path is what makes a
// cold start possible. Nothing here reads window titles, documents or user content.
internal sealed class InstalledAppChoice
{
    public string DisplayName { get; private set; }
    public string LaunchTarget { get; private set; }
    public string ProcessName { get; private set; }
    public Icon Icon { get; internal set; }
    public string Arguments { get; internal set; }
    public bool Running { get; set; }

    internal InstalledAppChoice(string displayName, string launchTarget, string processName)
    {
        DisplayName = displayName;
        LaunchTarget = launchTarget;
        ProcessName = processName;
    }

    public override string ToString()
    {
        return Running ? DisplayName + "   （正在运行）" : DisplayName + "   （未运行）";
    }
}

internal static class InstalledAppCatalog
{
    internal static string StoreDiagnostic = "";
    private static readonly string[] SkipWords =
    {
        "uninstall", "卸载", "readme", "read me", "help", "帮助", "documentation", "文档",
        "website", "网址", "homepage", "license", "许可", "release notes", "更新日志",
        "configuration", "配置工具", "cmd", "command prompt", "windows powershell"
    };

    // Enumerates start-menu shortcuts from both the machine and the current user, resolving
    // each shortcut to its launch target. Result is de-duplicated by target and sorted by
    // display name so the list stays stable between openings.
    internal static IList<InstalledAppChoice> List()
    {
        var found = new List<InstalledAppChoice>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        try
        {
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
        }
        catch { }
        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            string programs = Path.Combine(root, "Programs");
            Collect(programs, found, seenTargets, 0);
        }
        // Store applications come from the shell namespace, not from a start-menu
        // shortcut, so this ran once per start-menu root: the second pass re-walked the
        // whole AppsFolder and only ever added the diagnostic line again.
        CollectStoreApps(found, seenTargets);
        found.Sort(delegate(InstalledAppChoice left, InstalledAppChoice right)
        {
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });
        return found;
    }

    // Store / UWP applications are not in the start menu: they come from the shell's
    // AppsFolder and are launched through their AppUserModelID.
    //
    // The AppUserModelID is not a process name. Using its package part as one produced
    // an entry named after the package ("microsoft.windowsnotepad_11.2604..."), which
    // matched no process, so the application was offered a second time next to the copy
    // that was already running and could never be attached to. For anything running now
    // the real process name is asked of the operating system; the package-derived key is
    // only kept as a list key for an application that is not running, and the add flow
    // resolves the real name from the process once it has been started.
    private static void CollectStoreApps(List<InstalledAppChoice> found, HashSet<string> seenTargets)
    {
        object shell = null;
        object folder = null;
        object items = null;
        var samples = new StringBuilder();
        int kept = 0;
        IDictionary<string, string> running = PackagedAppIdentity.RunningPackagedProcesses();
        try
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) { StoreDiagnostic = "no_shell_com"; return; }
            shell = Activator.CreateInstance(shellType);
            folder = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell,
                new object[] { "shell:AppsFolder" });
            if (folder == null) { StoreDiagnostic = "no_apps_folder"; return; }
            items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
            if (items == null) { StoreDiagnostic = "no_items"; return; }
            int count = (int)items.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, items, null);
            StoreDiagnostic = StoreDiagnostic + " items=" + count + " packagedRunning=" + running.Count;
            for (int index = 0; index < count; index++)
            {
                object item = items.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, items,
                    new object[] { index });
                if (item == null) continue;
                string name = (item.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, item, null) as string ?? "").Trim();
                string path = (item.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, item, null) as string ?? "").Trim();
                string aumid = ReadExtendedProperty(item, "System.AppUserModel.ID");
                if (samples.Length < 200)
                    samples.Append("[").Append(name).Append("|").Append(path).Append("|").Append(aumid).Append("]");
                string identity = aumid.Length > 0 ? aumid : path.Replace("shell:AppsFolder\\", "");
                int bang = identity.IndexOf('!');
                if (name.Length == 0 || identity.Length == 0) continue;
                if (IsSkipped(name)) continue;
                string processName = ResolveStoreProcessName(identity, running, bang);
                if (processName.Length == 0) continue;
                string launchTarget = "shell:AppsFolder\\" + identity;
                if (!seenTargets.Add(launchTarget)) continue;
                var choice = new InstalledAppChoice(name, launchTarget, processName);
                choice.Icon = LoadStoreIcon(launchTarget);
                found.Add(choice);
                kept++;
            }
        }
        catch (Exception ex)
        {
            StoreDiagnostic = StoreDiagnostic + " error=" + ex.GetType().Name + ":" + ex.Message;
        }
        finally
        {
            if (items != null && Marshal.IsComObject(items)) Marshal.ReleaseComObject(items);
            if (folder != null && Marshal.IsComObject(folder)) Marshal.ReleaseComObject(folder);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            StoreDiagnostic = StoreDiagnostic + " kept=" + kept;
            if (samples.Length > 0) StoreDiagnostic = StoreDiagnostic + " samples=" + samples;
        }
    }

    // The real process name when the packaged application is running right now, so it
    // lines up with the running-application list and is de-duplicated against it;
    // otherwise the package part of the AppUserModelID, which is only a list key.
    private static string ResolveStoreProcessName(string identity,
        IDictionary<string, string> running, int bang)
    {
        string resolved;
        if (running.TryGetValue(identity, out resolved)) return resolved;
        string package = bang > 0 ? identity.Substring(0, bang) : identity;
        foreach (KeyValuePair<string, string> entry in running)
        {
            if (entry.Key.StartsWith(package + "!", StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        return FocusTargetDescriptor.NormalizeProcessName(package);
    }

    private static string ReadExtendedProperty(object item, string propertyName)
    {
        try
        {
            object extended = item.GetType().InvokeMember("ExtendedProperty", BindingFlags.InvokeMethod, null,
                item, new object[] { propertyName });
            return (extended as string ?? "").Trim();
        }
        catch { return ""; }
    }

    private static void Collect(string directory, List<InstalledAppChoice> found,
        HashSet<string> seenTargets, int depth)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        string[] shortcuts;
        try { shortcuts = Directory.GetFiles(directory, "*.lnk"); }
        catch { shortcuts = new string[0]; }
        foreach (string shortcut in shortcuts)
        {
            InstalledAppChoice choice = Resolve(shortcut);
            if (choice == null) continue;
            if (!seenTargets.Add(choice.LaunchTarget)) continue;
            found.Add(choice);
        }
        string[] children;
        try { children = Directory.GetDirectories(directory); }
        catch { children = new string[0]; }
        foreach (string child in children) Collect(child, found, seenTargets, depth + 1);
    }

    private static InstalledAppChoice Resolve(string shortcutPath)
    {
        string name = Path.GetFileNameWithoutExtension(shortcutPath);
        if (string.IsNullOrWhiteSpace(name) || IsSkipped(name)) return null;
        string target = ResolveShortcutTarget(shortcutPath);
        if (string.IsNullOrWhiteSpace(target)) return null;
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        string fileName = Path.GetFileName(target);
        string processName = FocusTargetDescriptor.NormalizeProcessName(Path.GetFileNameWithoutExtension(fileName));
        if (processName.Length == 0 || IsSkipped(processName)) return null;
        var choice = new InstalledAppChoice(ReadableName(name, target), target, processName);
        choice.Icon = LoadIcon(target);
        choice.Arguments = ResolveShortcutArguments(shortcutPath);
        return choice;
    }

    // The start-menu file name is not what the product calls itself ("Google_Chrome",
    // "IDLE_(Python_3.13_64-bit)"), so the executable's own description is preferred and
    // the shortcut name is only a cleaned-up fallback.
    // Whatever the source of a name is (shell link file name, FileDescription, ProductName),
    // it goes through the same cleanup: underscores and registered/trademark marks removed and
    // runs of spaces collapsed, so the list never shows "Google_Chrome" or "Microsoft®".
    private static string Tidy(string text)
    {
        string cleaned = (text ?? "").Replace('_', ' ').Replace("®", "").Replace("™", "").Replace("(R)", "");
        while (cleaned.IndexOf("  ", StringComparison.Ordinal) >= 0)
            cleaned = cleaned.Replace("  ", " ");
        return cleaned.Trim();
    }
    private static string ReadableName(string shortcutName, string exePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            string described = Tidy(info.FileDescription);
            if (described.Length > 0 && described.Length <= 60) return described;
            string product = Tidy(info.ProductName);
            if (product.Length > 0 && product.Length <= 60) return product;
        }
        catch { }
        string cleaned = Tidy(shortcutName);
        return cleaned.Length == 0 ? Tidy(shortcutName) : cleaned;
    }

    private static Icon LoadIcon(string exePath)
    {
        try { return Icon.ExtractAssociatedIcon(exePath); }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [Flags]
    private enum ShellItemImageFlags
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        IconOnly = 0x04
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellItemImageFlags flags, out IntPtr bitmapHandle);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string parsingName, IntPtr bindContext,
        ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object shellItem);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    // A packaged application has no executable on disk to take an icon from, so its
    // icon is asked of the shell for the AppsFolder item itself. Without this every
    // Store entry was listed with a blank gap where the other applications show theirs.
    private static Icon LoadStoreIcon(string launchTarget)
    {
        if (!PackagedAppIdentity.IsStoreLaunchTarget(launchTarget)) return null;
        IntPtr bitmapHandle = IntPtr.Zero;
        object item = null;
        try
        {
            Guid interfaceId = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(launchTarget, IntPtr.Zero, ref interfaceId, out item);
            var factory = item as IShellItemImageFactory;
            if (factory == null) return null;
            var size = new NativeSize { Width = 32, Height = 32 };
            if (factory.GetImage(size, ShellItemImageFlags.IconOnly | ShellItemImageFlags.BiggerSizeOk,
                    out bitmapHandle) != 0 || bitmapHandle == IntPtr.Zero) return null;
            using (Bitmap bitmap = Bitmap.FromHbitmap(bitmapHandle))
            {
                IntPtr iconHandle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(iconHandle).Clone(); }
                finally { DestroyIcon(iconHandle); }
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (item != null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item);
            if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
        }
    }
    private static bool IsSkipped(string text)
    {
        string lowered = (text ?? "").ToLowerInvariant();
        foreach (string word in SkipWords)
        {
            if (lowered.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // Resolved through the shell link object, the same way Explorer does it. Reflection keeps
    // this independent of any COM interop assembly.
    // Shortcuts may carry arguments (for example a launcher that opens a specific window);
    // they are remembered so a cold start reproduces what the user actually clicked.
    private static string ResolveShortcutArguments(string shortcutPath)
    {
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return "";
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            if (shortcut == null) return "";
            object value = shortcut.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null,
                shortcut, null);
            return (value as string ?? "").Trim();
        }
        catch
        {
            return "";
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
    }

    private static string ResolveShortcutTarget(string shortcutPath)
    {
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return "";
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            if (shortcut == null) return "";
            object value = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null,
                shortcut, null);
            return value as string ?? "";
        }
        catch
        {
            return "";
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
    }
}
