using System;
using System.Collections.Generic;

internal sealed class BrowserRemoteMappingChange
{
    public string Key { get; private set; }
    public string PreviousAction { get; private set; }
    public string RecommendedAction { get; private set; }

    internal BrowserRemoteMappingChange(string key, string previousAction, string recommendedAction)
    {
        Key = key ?? "";
        PreviousAction = previousAction ?? "";
        RecommendedAction = recommendedAction ?? "";
    }
}

internal sealed class BrowserRemotePlan
{
    private readonly Dictionary<string, string> current;
    private readonly Dictionary<string, string> recommended;

    public string ProfileId { get; private set; }
    public string ErrorCode { get; private set; }
    public IList<BrowserRemoteMappingChange> Changes { get; private set; }
    public bool IsValid { get { return string.IsNullOrWhiteSpace(ErrorCode); } }

    internal BrowserRemotePlan(string profileId, Dictionary<string, string> currentMappings,
        Dictionary<string, string> recommendedMappings, IList<BrowserRemoteMappingChange> changes,
        string errorCode)
    {
        ProfileId = profileId ?? "";
        current = Clone(currentMappings);
        recommended = Clone(recommendedMappings);
        Changes = changes ?? new List<BrowserRemoteMappingChange>();
        ErrorCode = errorCode ?? "";
    }

    internal Dictionary<string, string> ApplyTo(IDictionary<string, string> source)
    {
        Dictionary<string, string> applied = Clone(source);
        if (!IsValid) return applied;
        foreach (KeyValuePair<string, string> pair in recommended) applied[pair.Key] = pair.Value;
        return applied;
    }

    internal bool MatchesCurrent(IDictionary<string, string> source)
    {
        if (!IsValid) return false;
        foreach (KeyValuePair<string, string> pair in current)
        {
            string value;
            if (source == null || !source.TryGetValue(pair.Key, out value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    internal Dictionary<string, string> CurrentMappings()
    {
        return Clone(current);
    }

    internal Dictionary<string, string> RecommendedMappings()
    {
        return Clone(recommended);
    }

    private static Dictionary<string, string> Clone(IDictionary<string, string> source)
    {
        var clone = new Dictionary<string, string>();
        if (source == null) return clone;
        foreach (KeyValuePair<string, string> pair in source) clone[pair.Key] = pair.Value;
        return clone;
    }
}

internal static class BrowserProfileTemplate
{
    internal const string ProfileId = "browser-ai";

    private static readonly string[] ManagedKeys = {
        "确认键", "功能键", "功能键:short", "功能键:long", "上键", "下键", "左键", "右键"
    };

    internal static BrowserRemotePlan CreatePlan(string profileId,
        IDictionary<string, string> currentMappings, string rightChoice, string functionLongChoice)
    {
        string rightAction = RightAction(rightChoice);
        string functionLongAction = FunctionLongAction(functionLongChoice);
        if (!string.Equals(profileId, ProfileId, StringComparison.OrdinalIgnoreCase))
            return Invalid(profileId, "BROWSER-PROFILE-MISSING");
        if (currentMappings == null)
            return Invalid(profileId, "BROWSER-PROFILE-MAPPINGS-MISSING");
        if (rightAction.Length == 0 || functionLongAction.Length == 0)
            return Invalid(profileId, "BROWSER-TEMPLATE-OPTION-INVALID");

        var current = new Dictionary<string, string>();
        foreach (string key in ManagedKeys)
        {
            string value;
            current[key] = currentMappings.TryGetValue(key, out value) ? value ?? "" : "";
        }
        var recommended = new Dictionary<string, string>
        {
            { "确认键", "enter" },
            { "功能键", "shortcut:ctrl+r" },
            { "功能键:short", "shortcut:ctrl+r" },
            { "功能键:long", functionLongAction },
            { "上键", "pageup" },
            { "下键", "pagedown" },
            { "左键", "browserback" },
            { "右键", rightAction }
        };
        var changes = new List<BrowserRemoteMappingChange>();
        foreach (string key in ManagedKeys)
            if (!string.Equals(current[key], recommended[key], StringComparison.Ordinal))
                changes.Add(new BrowserRemoteMappingChange(key, current[key], recommended[key]));
        return new BrowserRemotePlan(ProfileId, current, recommended, changes, "");
    }

    internal static bool IsManagedKey(string key)
    {
        return Array.IndexOf(ManagedKeys, key ?? "") >= 0;
    }

    internal static bool IsAllowedAppliedAction(string key, string action)
    {
        string value = action ?? "";
        if (key == "确认键") return value == "enter";
        if (key == "功能键" || key == "功能键:short") return value == "shortcut:ctrl+r";
        if (key == "功能键:long")
            return value == "shortcut:ctrl+l" || value == "shortcut:ctrl+f";
        if (key == "上键") return value == "pageup";
        if (key == "下键") return value == "pagedown";
        if (key == "左键") return value == "browserback";
        if (key == "右键")
            return value == "tab" || value == "shortcut:ctrl+tab" ||
                value == "shortcut:browserforward";
        return false;
    }

    internal static bool IsTestableAction(string action)
    {
        string value = action ?? "";
        return value == "enter" || value == "pageup" || value == "pagedown" ||
            value == "browserback" || value == "tab" ||
            value == "shortcut:ctrl+r" || value == "shortcut:ctrl+l" ||
            value == "shortcut:ctrl+f" || value == "shortcut:ctrl+tab" ||
            value == "shortcut:browserforward";
    }

    private static BrowserRemotePlan Invalid(string profileId, string errorCode)
    {
        return new BrowserRemotePlan(profileId, null, null,
            new List<BrowserRemoteMappingChange>(), errorCode);
    }

    private static string RightAction(string choice)
    {
        if (string.Equals(choice, "tab", StringComparison.OrdinalIgnoreCase)) return "tab";
        if (string.Equals(choice, "next-tab", StringComparison.OrdinalIgnoreCase))
            return "shortcut:ctrl+tab";
        if (string.Equals(choice, "forward", StringComparison.OrdinalIgnoreCase))
            return "shortcut:browserforward";
        return "";
    }

    private static string FunctionLongAction(string choice)
    {
        if (string.Equals(choice, "address-bar", StringComparison.OrdinalIgnoreCase))
            return "shortcut:ctrl+l";
        if (string.Equals(choice, "find", StringComparison.OrdinalIgnoreCase))
            return "shortcut:ctrl+f";
        return "";
    }
}

internal sealed class BrowserRemoteUndoSnapshot
{
    public int SchemaVersion { get; set; }
    public string ProfileId { get; set; }
    public string CreatedUtc { get; set; }
    public Dictionary<string, string> PreviousMappings { get; set; }
    public Dictionary<string, string> AppliedMappings { get; set; }

    internal static BrowserRemoteUndoSnapshot FromPlan(BrowserRemotePlan plan)
    {
        var previous = new Dictionary<string, string>();
        var applied = new Dictionary<string, string>();
        if (plan != null && plan.IsValid)
            foreach (BrowserRemoteMappingChange change in plan.Changes)
            {
                previous[change.Key] = change.PreviousAction;
                applied[change.Key] = change.RecommendedAction;
            }
        return new BrowserRemoteUndoSnapshot
        {
            SchemaVersion = 1,
            ProfileId = plan == null ? "" : plan.ProfileId,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            PreviousMappings = previous,
            AppliedMappings = applied
        };
    }

    internal bool TryValidate(out string errorCode)
    {
        errorCode = "";
        if (SchemaVersion > 1) { errorCode = "BROWSER-UNDO-SCHEMA-NEWER"; return false; }
        if (SchemaVersion != 1 || !string.Equals(ProfileId, BrowserProfileTemplate.ProfileId,
            StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "BROWSER-UNDO-INVALID";
            return false;
        }
        if (PreviousMappings == null || AppliedMappings == null ||
            PreviousMappings.Count == 0 || PreviousMappings.Count != AppliedMappings.Count)
        {
            errorCode = "BROWSER-UNDO-INVALID";
            return false;
        }
        foreach (KeyValuePair<string, string> pair in AppliedMappings)
        {
            string previous;
            if (!BrowserProfileTemplate.IsManagedKey(pair.Key) ||
                !BrowserProfileTemplate.IsAllowedAppliedAction(pair.Key, pair.Value) ||
                !PreviousMappings.TryGetValue(pair.Key, out previous))
            {
                errorCode = "BROWSER-UNDO-INVALID";
                return false;
            }
        }
        return true;
    }

    internal bool TryRestore(IDictionary<string, string> currentMappings,
        out Dictionary<string, string> restored, out string errorCode)
    {
        restored = Clone(currentMappings);
        if (!TryValidate(out errorCode)) return false;
        foreach (KeyValuePair<string, string> pair in AppliedMappings)
        {
            string current;
            if (currentMappings == null || !currentMappings.TryGetValue(pair.Key, out current) ||
                !string.Equals(current, pair.Value, StringComparison.Ordinal))
            {
                errorCode = "BROWSER-UNDO-CONFLICT";
                return false;
            }
        }
        foreach (KeyValuePair<string, string> pair in PreviousMappings) restored[pair.Key] = pair.Value;
        errorCode = "";
        return true;
    }

    private static Dictionary<string, string> Clone(IDictionary<string, string> source)
    {
        var clone = new Dictionary<string, string>();
        if (source != null)
            foreach (KeyValuePair<string, string> pair in source) clone[pair.Key] = pair.Value;
        return clone;
    }
}
