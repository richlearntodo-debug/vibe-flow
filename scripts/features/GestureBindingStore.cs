using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Persistence for the remote gesture layers: one entry per physical key holding the three
// layer bindings (short / long / double) plus an optional macro — an ordered list of
// actions executed as one gesture. Nothing here stores transcription text or window
// titles; actions are the app's own identifiers.
internal sealed class GestureLayerEntry
{
    public string key { get; set; }
    public string shortAction { get; set; }
    public string longAction { get; set; }
    public string doubleAction { get; set; }
    public string macroName { get; set; }
    public List<string> macroSteps { get; set; }
}

internal sealed class GestureLayerDocument
{
    public int schemaVersion { get; set; }
    public List<GestureLayerEntry> layers { get; set; }
}

internal sealed class GestureBindingStore
{
    private const int SchemaVersion = 1;
    // A gesture macro stays useful and reviewable at this size; longer chains belong in a
    // profile rather than on one key press.
    internal const int MaxMacroSteps = 8;
    // Shown on a layer that has no binding of its own.
    internal const string UnboundLayerText = "未设置";
    // How a fallback reads in the cards: "跟随短按" — plain words, because 回退 read as jargon.
    internal const string FallbackPrefix = "跟随";
    internal const string MacroPrefix = "宏：";

    private readonly string path;
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    internal GestureBindingStore(string userStateRoot)
    {
        path = Path.Combine(userStateRoot, "gesture-layers.json");
    }

    internal string FilePath { get { return path; } }

    // The recommended layer table for a machine that has none yet. Until this existed, a fresh
    // install had an empty table, so the whole three-layer gesture surface shipped invisible:
    // every 长按 / 双击 row read 未配置 and the user had to author the table by hand before any
    // of the gesture work did anything. Every binding below was verified on a real RC003.
    //
    // Two properties are deliberate:
    //
    // - Home carries nothing. Its short press is 显示桌面 (win+d), and the first tap of a double
    //   tap executes the short layer, so a Home double tap hides the desktop and then cannot be
    //   completed at double-tap speed (measured on real hardware: the second tap arrived 2235 ms
    //   later, so the double never formed). Shipping that would ship a binding that cannot be used.
    // - No key's short press is redefined here. Where a shipped Profile already binds the same
    //   action to a key's short press (浏览器 AI: 上/下 = pageup/pagedown, 左 = browserback) the
    //   long layer simply agrees with it; nothing becomes unreachable and no Profile changes.
    internal static GestureLayerDocument DefaultDocument()
    {
        var document = new GestureLayerDocument
        {
            schemaVersion = SchemaVersion,
            layers = new List<GestureLayerEntry>()
        };
        // Long press scrolls and steps back; double tap is the editing and media layer.
        UpsertLayer(document, "up", GestureKind.Long, "pageup");
        UpsertLayer(document, "up", GestureKind.Double, "ctrl+x");
        UpsertLayer(document, "down", GestureKind.Long, "pagedown");
        UpsertLayer(document, "down", GestureKind.Double, "ctrl+a");
        UpsertLayer(document, "left", GestureKind.Long, "browserback");
        UpsertLayer(document, "left", GestureKind.Double, "ctrl+z");
        UpsertLayer(document, "right", GestureKind.Long, "ctrl+shift+z");
        UpsertLayer(document, "right", GestureKind.Double, "ctrl+s");
        UpsertLayer(document, "ok", GestureKind.Long, "volumemute");
        UpsertLayer(document, "ok", GestureKind.Double, "mediaplaypause");
        // 功能键 and Home own their long layer in the per-Profile mapping table, so a store long
        // layer for them would be written and then ignored; only their double layer is offered.
        UpsertLayer(document, "menu", GestureKind.Double, "volumeup");
        UpsertLayer(document, "tv", GestureKind.Long, "launch-client:chatgpt");
        UpsertLayer(document, "tv", GestureKind.Double, "volumedown");
        return document;
    }

    internal GestureLayerDocument Load()
    {
        try
        {
            if (!File.Exists(path))
                return new GestureLayerDocument { schemaVersion = SchemaVersion, layers = new List<GestureLayerEntry>() };
            GestureLayerDocument document = serializer.Deserialize<GestureLayerDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.schemaVersion > SchemaVersion)
                return new GestureLayerDocument { schemaVersion = SchemaVersion, layers = new List<GestureLayerEntry>() };
            if (document.layers == null) document.layers = new List<GestureLayerEntry>();
            return document;
        }
        catch
        {
            return new GestureLayerDocument { schemaVersion = SchemaVersion, layers = new List<GestureLayerEntry>() };
        }
    }

    internal bool TrySave(GestureLayerDocument document)
    {
        if (document == null) return false;
        try
        {
            document.schemaVersion = SchemaVersion;
            if (document.layers == null) document.layers = new List<GestureLayerEntry>();
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

    internal static GestureLayerEntry Find(GestureLayerDocument document, string key)
    {
        if (document == null || document.layers == null || string.IsNullOrWhiteSpace(key)) return null;
        string normalized = NormalizeKey(key);
        foreach (GestureLayerEntry entry in document.layers)
        {
            if (entry != null && string.Equals(NormalizeKey(entry.key), normalized, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    internal static string NormalizeKey(string key)
    {
        return (key ?? "").Trim().ToLowerInvariant();
    }

    // Upserts one layer binding for a key (null or empty clears that layer).
    internal static GestureLayerEntry UpsertLayer(GestureLayerDocument document, string key, GestureKind kind, string action)
    {
        if (document == null || string.IsNullOrWhiteSpace(key)) return null;
        if (document.layers == null) document.layers = new List<GestureLayerEntry>();
        GestureLayerEntry entry = findOrCreate(document, key);
        string normalizedAction = GestureLayerPolicy.NormalizeAction(action);
        switch (kind)
        {
            case GestureKind.Long: entry.longAction = normalizedAction; break;
            case GestureKind.Double: entry.doubleAction = normalizedAction; break;
            default: entry.shortAction = normalizedAction; break;
        }
        return entry;
    }

    private static GestureLayerEntry findOrCreate(GestureLayerDocument document, string key)
    {
        GestureLayerEntry entry = Find(document, key);
        if (entry != null) return entry;
        entry = new GestureLayerEntry { key = NormalizeKey(key) };
        document.layers.Add(entry);
        return entry;
    }

    internal static GestureBinding ToBinding(GestureLayerEntry entry)
    {
        if (entry == null) return null;
        return new GestureBinding
        {
            Key = entry.key,
            ShortAction = entry.shortAction,
            LongAction = entry.longAction,
            DoubleAction = entry.doubleAction
        };
    }

    internal static string OwnAction(GestureLayerEntry entry, GestureKind kind)
    {
        if (entry == null) return "";
        switch (kind)
        {
            case GestureKind.Long: return GestureLayerPolicy.NormalizeAction(entry.longAction);
            case GestureKind.Double: return GestureLayerPolicy.NormalizeAction(entry.doubleAction);
            default: return GestureLayerPolicy.NormalizeAction(entry.shortAction);
        }
    }

    // Which layer an unbound layer actually uses: double falls back to long and then short,
    // long falls back to short. Short has nowhere to go and returns itself.
    internal static GestureKind FallbackKind(GestureLayerEntry entry, GestureKind kind)
    {
        if (kind == GestureKind.Double)
        {
            if (GestureLayerPolicy.NormalizeAction(entry == null ? "" : entry.longAction).Length > 0) return GestureKind.Long;
            return GestureKind.Short;
        }
        return GestureKind.Short;
    }

    // The text a gesture card shows for one layer. A bound layer shows its own action (or
    // its macro name); an unbound layer says so and names the layer it falls back to, so the
    // user is never told a gesture does something it does not do.
    internal static string DescribeLayer(GestureLayerEntry entry, GestureKind kind, Func<string, string> actionText)
    {
        if (kind == GestureKind.Double && HasMacro(entry))
            return MacroPrefix + entry.macroName;
        string own = OwnAction(entry, kind);
        if (own.Length > 0) return actionText == null ? own : actionText(own);
        string fallback = GestureLayerPolicy.ActionFor(ToBinding(entry), kind);
        if (fallback.Length == 0) return UnboundLayerText;
        string fallbackName = actionText == null ? fallback : actionText(fallback);
        return UnboundLayerText + "（" + FallbackPrefix + GestureLayerPolicy.Describe(FallbackKind(entry, kind)) +
            "：" + fallbackName + "）";
    }

    // A macro is valid when it has a name, at least one step, no blank steps and no more
    // than MaxMacroSteps entries. Returns "" when valid, otherwise a reason code.
    internal static string ValidateMacro(string macroName, IList<string> steps)
    {
        if (steps == null || steps.Count == 0) return "GESTURE-MACRO-EMPTY";
        if (steps.Count > MaxMacroSteps) return "GESTURE-MACRO-TOO-LONG";
        for (int index = 0; index < steps.Count; index++)
        {
            if (GestureLayerPolicy.NormalizeAction(steps[index]).Length == 0) return "GESTURE-MACRO-BLANK-STEP";
        }
        if ((macroName ?? "").Trim().Length == 0) return "GESTURE-MACRO-NO-NAME";
        return "";
    }

    internal static GestureLayerEntry AttachMacro(GestureLayerDocument document, string key, string macroName,
        IList<string> steps, out string errorCode)
    {
        errorCode = ValidateMacro(macroName, steps);
        if (errorCode.Length > 0) return null;
        if (document == null || string.IsNullOrWhiteSpace(key))
        {
            errorCode = "GESTURE-KEY-INVALID";
            return null;
        }
        if (document.layers == null) document.layers = new List<GestureLayerEntry>();
        GestureLayerEntry entry = findOrCreate(document, key);
        entry.macroName = macroName.Trim();
        var copy = new List<string>();
        foreach (string step in steps) copy.Add(GestureLayerPolicy.NormalizeAction(step));
        entry.macroSteps = copy;
        return entry;
    }

    internal static bool HasMacro(GestureLayerEntry entry)
    {
        if (entry == null || entry.macroSteps == null || entry.macroSteps.Count == 0) return false;
        return ValidateMacro(entry.macroName, entry.macroSteps).Length == 0;
    }

    // The actions a gesture actually runs, in order: a bound macro wins over the single
    // action of that layer, so one gesture can drive a short sequence.
    internal static IList<string> ResolveSteps(GestureLayerEntry entry, GestureKind kind)
    {
        var steps = new List<string>();
        if (entry == null) return steps;
        if (kind == GestureKind.Double && HasMacro(entry))
        {
            foreach (string step in entry.macroSteps) steps.Add(GestureLayerPolicy.NormalizeAction(step));
            return steps;
        }
        string action = GestureLayerPolicy.ActionFor(ToBinding(entry), kind);
        if (action.Length > 0) steps.Add(action);
        return steps;
    }
}
