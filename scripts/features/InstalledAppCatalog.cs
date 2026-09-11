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
        "configuration", "配置工具", "cmd", "command prompt", "windows powershell",
        // Bare shells, added after measuring: a binding named "powershell" reached the workflow list because the
        // entry above only matches the phrase "windows powershell". A shell is not a place to dictate text into,
        // which is the same judgement the phrase was already making.
        "powershell", "pwsh",
        // Documentation, not a place to type. Measured on this machine: "Inno Setup FAQ" was offered as a target
        // while "Inno Setup Documentation" was skipped, so the list depended on which word an installer happened to
        // choose. Expected effect of adding these two: one fewer candidate on this machine, 95 -> 94.
        "faq", "frequently asked"
    };

    // Enumerates start-menu shortcuts from both the machine and the current user, resolving
    // each shortcut to its launch target. Result is de-duplicated by target and sorted by
    // display name so the list stays stable between openings.
    internal static IList<InstalledAppChoice> List()
    {
        var found = new List<InstalledAppChoice>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // The process name is the application's identity across both sources: shell:AppsFolder lists
        // desktop applications as well as packaged ones, so an application that already arrived
        // through its start-menu shortcut would otherwise be offered a second time under its shell
        // identity (measured on this machine: CatProX was listed twice, both times from
        // D:\CatproX\CatproX.exe).
        var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            Collect(programs, found, seenTargets, seenProcesses, 0);
        }
        // Store applications come from the shell namespace, not from a start-menu
        // shortcut, so this ran once per start-menu root: the second pass re-walked the
        // whole AppsFolder and only ever added the diagnostic line again.
        CollectStoreApps(found, seenTargets, seenProcesses);
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
    private static void CollectStoreApps(List<InstalledAppChoice> found, HashSet<string> seenTargets,
        HashSet<string> seenProcesses)
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
                string processName = ResolveStoreProcessName(identity, running, bang,
                    ReadExtendedProperty(item, "System.Link.TargetParsingPath"));
                if (processName.Length == 0) continue;
                if (!seenProcesses.Add(processName)) continue;
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
    // otherwise the executable behind the shell entry, and only then the package part of
    // the AppUserModelID, which is not a process name at all.
    private static string ResolveStoreProcessName(string identity,
        IDictionary<string, string> running, int bang, string desktopTarget)
    {
        string resolved;
        if (running.TryGetValue(identity, out resolved)) return resolved;
        string package = bang > 0 ? identity.Substring(0, bang) : identity;
        foreach (KeyValuePair<string, string> entry in running)
        {
            if (entry.Key.StartsWith(package + "!", StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        // A desktop application may register its own AppUserModelID ("org.erb.vortex") and appear in
        // the AppsFolder under it. That identifier is not a process name, so an application which
        // already arrived through its start-menu shortcut was offered a second time under the
        // identifier (measured: CatProX listed twice, once as catprox and once as org.erb.vortex).
        // The shell exposes the executable behind such an entry — and exposes nothing for a real
        // packaged application — so the real process name is used and the duplicate collapses.
        string fromDesktopTarget = FocusTargetDescriptor.NormalizeProcessName(desktopTarget);
        if (fromDesktopTarget.Length > 0) return fromDesktopTarget;
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
        HashSet<string> seenTargets, HashSet<string> seenProcesses, int depth)
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
            seenProcesses.Add(choice.ProcessName);
            found.Add(choice);
        }
        string[] children;
        try { children = Directory.GetDirectories(directory); }
        catch { children = new string[0]; }
        foreach (string child in children) Collect(child, found, seenTargets, seenProcesses, depth + 1);
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
        choice.Icon = IconForExecutable(target);
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

    // Extract first, then ask the shell. An executable can carry no icon resource of its own and
    // still show one in Explorer, so ExtractAssociatedIcon alone leaves real rows blank — measured
    // on this machine: Steam and BOOTICE both have no icon to extract and both show one in the
    // start menu. The shell answer is the same one Explorer draws.
    internal static Icon IconForExecutable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        Icon extracted = File.Exists(exePath) ? LoadIcon(exePath) : null;
        return extracted ?? LoadShellImage(exePath);
    }

    // The executable behind a running process, for an icon. Readable only for processes this
    // session may inspect; anything else returns nothing rather than guessing.
    internal static string ExecutableForProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return "";
        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    string path = process.MainModule == null ? "" : process.MainModule.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
                }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }
        return "";
    }

    // Whether an application actually exists on this machine, by process name.
    //
    // The 工作流 page is rebuilt on every navigation and has to answer this for each configured binding, while the
    // scan behind List() walks both start-menu roots and enumerates the shell AppsFolder. The answer only changes
    // when the user installs something, so it is cached; the picker still calls List() directly and stays fresh.
    private static readonly Dictionary<string, bool> installedByProcess =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static DateTime installedCacheBuiltAt = DateTime.MinValue;

    internal static bool IsInstalled(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string key = processName.Trim();
        // Being *running* is deliberately not part of this answer, although it is the cheapest test available.
        // Measured: with a running-process shortcut, the workflow list admitted msedge, cmd and powershell — the
        // last two are exactly the entries the catalogue's own skip list excludes, and all three were present only
        // because this session had such processes running. Whether an application is the user's own is a question
        // about what is installed, so the catalogue answers it. A portable tool that is running but has no shortcut
        // can still be added as a favourite application, which is the user saying it is theirs.
        if (installedCacheBuiltAt == DateTime.MinValue ||
            (DateTime.UtcNow - installedCacheBuiltAt).TotalMinutes > 5)
        {
            RebuildInstalledCache();
        }
        bool installed;
        return installedByProcess.TryGetValue(key, out installed) && installed;
    }

    // The icon an application has in the machine's own catalogue, by process name.
    //
    // The workflow rows resolve their icon through this instead of through the running process: an application that
    // is installed but not running has no process to ask, and the rows were therefore falling back to a generated
    // tile even for applications whose icon the catalogue already holds. Measured on this machine, 91 of 95
    // catalogue entries carry an icon.
    private static readonly Dictionary<string, Icon> catalogueIcons =
        new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

    internal static Icon IconForProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;
        string key = processName.Trim();
        if (installedCacheBuiltAt == DateTime.MinValue ||
            (DateTime.UtcNow - installedCacheBuiltAt).TotalMinutes > 5)
        {
            RebuildInstalledCache();
        }
        Icon icon;
        return catalogueIcons.TryGetValue(key, out icon) ? icon : null;
    }

    private static void RebuildInstalledCache()
    {
        installedByProcess.Clear();
        catalogueIcons.Clear();
        try
        {
            foreach (InstalledAppChoice entry in List())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ProcessName)) continue;
                string name = entry.ProcessName.Trim();
                // A shortcut's name and the process it starts can differ in case ("Cursor" / "cursor"), and the
                // entries can carry an extension; the comparison is case-insensitive and extension-blind.
                installedByProcess[name] = true;
                if (entry.Icon != null) catalogueIcons[name] = entry.Icon;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string trimmed = name.Substring(0, name.Length - 4);
                    installedByProcess[trimmed] = true;
                    if (entry.Icon != null && !catalogueIcons.ContainsKey(trimmed)) catalogueIcons[trimmed] = entry.Icon;
                }
            }
        }
        catch
        {
            // An unavailable scan must not turn into "not installed": the list would silently empty itself, so
            // the failure is recorded and callers fall back to the running-process test above.
            installedByProcess.Clear();
            catalogueIcons.Clear();
        }
        installedCacheBuiltAt = DateTime.UtcNow;
    }

    // What the application calls itself, for an application that is running but is not in the
    // catalogue at all (a portable tool, a background helper): the same description-then-product
    // order the shortcut entries use, and "" when the file says nothing.
    internal static string DescribeExecutable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return "";
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            string described = Tidy(info.FileDescription);
            if (described.Length > 0 && described.Length <= 60) return described;
            string product = Tidy(info.ProductName);
            if (product.Length > 0 && product.Length <= 60) return product;
        }
        catch { }
        return "";
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
        return LoadShellImage(launchTarget);
    }

    // The shell's own image for a parsing name; an AppsFolder item and a file path both work.
    private static Icon LoadShellImage(string parsingName)
    {
        if (string.IsNullOrWhiteSpace(parsingName)) return null;
        IntPtr bitmapHandle = IntPtr.Zero;
        object item = null;
        try
        {
            Guid interfaceId = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref interfaceId, out item);
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
    // The same judgement the picker applies to its own rows, exposed so the workflow list can apply it to a
    // configured binding: a shell, an uninstaller or a read-me is not a place to dictate text into, and a profile's
    // application list can name one. Measured on this machine, cmd and powershell reached the workflow list from
    // config bindings even though this skip list excludes them from the add-application list.
    internal static bool IsSkipped(string text)
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

// One icon lookup for every surface that lists applications.
//
// The picker and the workflow rows drew their icons separately, and the workflow rows drew none at all — the page
// listed applications with a status dot where the logo belongs. Sharing the lookup keeps them from drifting, and
// the chain ends in a tile drawn from the name so a surface can never show an empty slot.
//
// Measured on this machine over the whole catalogue: 95 applications, 91 with an icon already resolved, 3 falling
// through to the tile, and 39 ms for all of them. The tile is the rare case, not the norm.
internal static class AppIcons
{
    private static readonly Dictionary<string, Image> cache =
        new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

    internal static Image For(string processName, string displayName, string launchTarget, Icon catalogueIcon,
        out string source)
    {
        string key = processName ?? "";
        Image cached;
        if (key.Length > 0 && cache.TryGetValue(key, out cached))
        {
            source = "cached";
            return cached;
        }
        Image resolved = null;
        source = "catalogue";
        try
        {
            if (catalogueIcon != null) resolved = catalogueIcon.ToBitmap();
            if (resolved == null)
            {
                source = "process";
                Icon fromProcess = InstalledAppCatalog.IconForExecutable(
                    InstalledAppCatalog.ExecutableForProcess(key));
                if (fromProcess != null) resolved = fromProcess.ToBitmap();
            }
            if (resolved == null)
            {
                source = "target";
                Icon fromTarget = InstalledAppCatalog.IconForExecutable(launchTarget);
                if (fromTarget != null) resolved = fromTarget.ToBitmap();
            }
        }
        catch
        {
            resolved = null;
        }
        if (resolved == null)
        {
            source = "tile";
            resolved = LetterTile(displayName);
        }
        if (key.Length > 0) cache[key] = resolved;
        return resolved;
    }

    // A generated tile for an application whose icon cannot be read: its first character on a colour derived from
    // its name, so the same application always gets the same tile.
    internal static Image LetterTile(string name)
    {
        const int size = 64;
        var bitmap = new Bitmap(size, size);
        string text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        Color[] palette =
        {
            Color.FromArgb(104, 82, 244), Color.FromArgb(0, 153, 190), Color.FromArgb(10, 164, 104),
            Color.FromArgb(229, 151, 39), Color.FromArgb(204, 70, 82), Color.FromArgb(80, 120, 220)
        };
        int hash = 0;
        foreach (char character in name ?? "") hash = (hash * 31 + character) & 0x7fffffff;
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = RoundedPath(new Rectangle(0, 0, size - 1, size - 1), 16))
            using (var brush = new SolidBrush(palette[hash % palette.Length]))
                graphics.FillPath(brush, path);
            using (var font = new Font("Microsoft YaHei UI", 26f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size), format);
            }
        }
        return bitmap;
    }

    // Icons are drawn over a subtle rounded tile. Many application icons are drawn for a white or a dark
    // background and carry transparency, so drawn straight onto a card some of them are nearly invisible —
    // measured, the ones that looked missing were largely this rather than an unresolved icon.
    internal static void DrawTile(Graphics graphics, Image icon, Rectangle bounds, Color tileColor, int radius)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = RoundedPath(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1),
            Math.Max(2, radius)))
        using (var fill = new SolidBrush(tileColor))
            graphics.FillPath(fill, path);
        if (icon == null) return;
        int inset = Math.Max(2, bounds.Width / 8);
        var target = new Rectangle(bounds.X + inset, bounds.Y + inset,
            Math.Max(1, bounds.Width - inset * 2), Math.Max(1, bounds.Height - inset * 2));
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(icon, target);
    }

    internal static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int diameter = Math.Max(2, radius * 2);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
