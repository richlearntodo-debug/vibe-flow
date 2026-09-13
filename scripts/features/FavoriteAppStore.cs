using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// The simple model the redesigned Smart Focus exposes: a short list of favourite
// applications, one of which is current. Dictation always targets the current
// favourite, no matter where the cursor is, and a favourite that is not running is
// started first. Nothing here stores text: only process name, readable name, the
// executable path (so it can be started) and the learned input-target id.
internal sealed class FavoriteApp
{
    public string processName { get; set; }
    public string displayName { get; set; }
    public string exePath { get; set; }
    public string targetId { get; set; }
    // The runtime contract between the two modes: a "workflow" entry can be made the current app and has its
    // learned input box focused before speaking, so the text is fixed to it; a "shortcut" entry is only
    // summoned by 打开 and never takes the text. Only the current app receives dictation either way.
    public string mode { get; set; }
    // Launch arguments remembered from the shortcut the user picked.
    public string arguments { get; set; }
    public string learnedAtUtc { get; set; }
}

internal sealed class FavoriteAppDocument
{
    public int schemaVersion { get; set; }
    public string selectedProcess { get; set; }
    public List<FavoriteApp> apps { get; set; }
}

internal sealed class FavoriteAppStore
{
    private const int SchemaVersion = 1;
    private readonly string path;
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    internal FavoriteAppStore(string userStateRoot)
    {
        path = Path.Combine(userStateRoot, "favorite-apps.json");
    }

    internal string FilePath { get { return path; } }

    internal FavoriteAppDocument Load()
    {
        try
        {
            if (!File.Exists(path)) return new FavoriteAppDocument { schemaVersion = SchemaVersion, apps = new List<FavoriteApp>() };
            FavoriteAppDocument document = serializer.Deserialize<FavoriteAppDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.schemaVersion > SchemaVersion)
                return new FavoriteAppDocument { schemaVersion = SchemaVersion, apps = new List<FavoriteApp>() };
            if (document.apps == null) document.apps = new List<FavoriteApp>();
            // An entry with no mode predates the mode contract — and until now every freshly learned entry
            // was written without one, which left stored data contradicting the row's own status line.
            // Default it to the workflow mode (the one that can receive the text) and heal the file once, so
            // the legacy value does not linger on disk forever.
            bool healed = false;
            foreach (FavoriteApp app in document.apps)
            {
                if (app == null) continue;
                if (FavoriteAppStatus.IsWorkflowMode(app.mode) || FavoriteAppStatus.IsShortcutMode(app.mode))
                    continue;
                app.mode = FavoriteAppStatus.WorkflowMode;
                healed = true;
            }
            if (healed) TrySave(document);
            return document;
        }
        catch
        {
            return new FavoriteAppDocument { schemaVersion = SchemaVersion, apps = new List<FavoriteApp>() };
        }
    }

    internal bool TrySave(FavoriteAppDocument document)
    {
        if (document == null) return false;
        try
        {
            document.schemaVersion = SchemaVersion;
            if (document.apps == null) document.apps = new List<FavoriteApp>();
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(document), new UTF8Encoding(false));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(temporary, path, true);
            File.Delete(temporary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Keeps the favourite list in step with the learned targets without any extra UI:
    // every verified target for a running process becomes a favourite entry, and the
    // selected entry stays valid.
    internal bool SyncFromTargets(IList<FocusTargetDescriptor> targets, string defaultTargetId,
        Func<string, string> executablePathLookup, out FavoriteAppDocument document)
    {
        document = Load();
        if (targets == null) return false;
        bool changed = false;
        foreach (FocusTargetDescriptor target in targets)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ProcessName)) continue;
            if (!target.LastVerifiedUtc.HasValue) continue;
            string processName = FocusTargetDescriptor.NormalizeProcessName(target.ProcessName);
            if (processName.Length == 0) continue;
            FavoriteApp existing = document.apps.Find(delegate(FavoriteApp item)
            {
                return item != null && string.Equals(item.processName, processName, StringComparison.OrdinalIgnoreCase);
            });
            string exePath = executablePathLookup == null ? null : executablePathLookup(processName);
            if (existing == null)
            {
                document.apps.Add(new FavoriteApp
                {
                    processName = processName,
                    displayName = string.IsNullOrWhiteSpace(target.Name) ? processName : target.Name,
                    exePath = exePath ?? "",
                    targetId = target.Id,
                    mode = FavoriteAppStatus.WorkflowMode,
                    learnedAtUtc = target.LastVerifiedUtc.Value.ToUniversalTime().ToString("o")
                });
                changed = true;
            }
            else
            {
                if (!FavoriteAppStatus.IsWorkflowMode(existing.mode) &&
                    !FavoriteAppStatus.IsShortcutMode(existing.mode))
                {
                    existing.mode = FavoriteAppStatus.WorkflowMode;
                    changed = true;
                }
                if (!string.Equals(existing.targetId, target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    existing.targetId = target.Id;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(exePath) &&
                    !string.Equals(existing.exePath, exePath, StringComparison.OrdinalIgnoreCase))
                {
                    existing.exePath = exePath;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(existing.displayName))
                {
                    existing.displayName = target.Name;
                    changed = true;
                }
            }
        }
        string selected = SelectedProcess(document, defaultTargetId, targets);
        if (!string.Equals(selected, document.selectedProcess, StringComparison.OrdinalIgnoreCase))
        {
            document.selectedProcess = selected;
            changed = true;
        }
        return changed;
    }

    // The current favourite is what dictation targets. It falls back to the document's
    // default target so an existing configuration keeps working after the redesign.
    internal static string SelectedProcess(FavoriteAppDocument document, string defaultTargetId,
        IList<FocusTargetDescriptor> targets)
    {
        if (document != null && document.apps != null && !string.IsNullOrWhiteSpace(document.selectedProcess))
        {
            FavoriteApp selected = document.apps.Find(delegate(FavoriteApp item)
            {
                return item != null && string.Equals(item.processName, document.selectedProcess,
                    StringComparison.OrdinalIgnoreCase);
            });
            if (selected != null) return selected.processName;
        }
        if (targets != null && !string.IsNullOrWhiteSpace(defaultTargetId))
        {
            foreach (FocusTargetDescriptor target in targets)
            {
                if (target != null && string.Equals(target.Id, defaultTargetId, StringComparison.OrdinalIgnoreCase))
                    return FocusTargetDescriptor.NormalizeProcessName(target.ProcessName);
            }
        }
        if (document != null && document.apps != null && document.apps.Count > 0)
            return document.apps[0].processName;
        return "";
    }

    internal static FavoriteApp Find(FavoriteAppDocument document, string processName)
    {
        if (document == null || document.apps == null || string.IsNullOrWhiteSpace(processName)) return null;
        string normalized = FocusTargetDescriptor.NormalizeProcessName(processName);
        foreach (FavoriteApp item in document.apps)
        {
            if (item != null && string.Equals(item.processName, normalized, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }
}
