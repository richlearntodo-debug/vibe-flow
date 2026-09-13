using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// Every action the shortcut pickers offer must be executable and must survive being stored.
//
// The failure mode this guards is silent: NormalizeShortcutProfileMappings replaces any action
// IsSupportedMappingAction rejects with that key's DEFAULT, so an action that looks selectable
// but is not supported quietly becomes a different action after a save — the user picks
// 「系统 · 音量增加」and later finds the key doing something else. Nothing about the UI says so.
internal static class ShortcutActionTests
{
    private static readonly string[] ProfileKeys = {
        "确认键", "Home", "Home:short", "Home:long", "TV", "功能键",
        "功能键:short", "功能键:long", "上键", "下键", "左键", "右键"
    };
    private static readonly string[] ExpectedStarterIds = {
        "general", "vibe-coding", "browser-ai", "terminal-agent"
    };
    private static readonly string SnippetManageAction = "snippet:manage";

    // Home and 功能键 are mirrors of their :short variants: NormalizeShortcutProfileMappings
    // assigns them from Home:short / 功能键:short, so a key and its partner must be written
    // together or the partner's default wins. That is the schema, not a defect.
    private static string[] WithMirror(string key, string action)
    {
        if (key == "Home") return new[] { "Home", "Home:short" };
        if (key == "功能键") return new[] { "功能键", "功能键:short" };
        return new[] { key };
    }

    private static int failures;

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + message);
    }

    private static MethodInfo Find(string name, int argumentCount)
    {
        foreach (MethodInfo method in typeof(VibeMicForm).GetMethods(
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (method.Name == name && method.GetParameters().Length == argumentCount) return method;
        }
        throw new InvalidOperationException("VibeMicForm." + name + " is missing");
    }

    private static object Call(string name, params object[] arguments)
    {
        return Find(name, arguments.Length).Invoke(null, arguments);
    }

    private static bool Supported(string action) { return (bool)Call("IsSupportedMappingAction", action); }
    private static bool Persistable(string action) { return (bool)Call("IsPersistableMappingAction", action); }
    private static string Text(string action) { return (string)Call("CustomActionText", action); }
    private static string Normalized(string key, string action)
    {
        return (string)Call("NormalizePhysicalMappingAction", key, action);
    }

    private sealed class Choice
    {
        public string Label;
        public string Action;
    }

    private static List<Choice> Choices(string method, params object[] arguments)
    {
        var result = new List<Choice>();
        var raw = Call(method, arguments) as IEnumerable;
        if (raw == null) return result;
        foreach (object item in raw)
        {
            if (item == null) continue;
            Type type = item.GetType();
            FieldInfo label = type.GetField("Label");
            FieldInfo shortcut = type.GetField("Shortcut");
            result.Add(new Choice
            {
                Label = label == null ? "" : Convert.ToString(label.GetValue(item)),
                Action = shortcut == null ? "" : Convert.ToString(shortcut.GetValue(item))
            });
        }
        return result;
    }

    // A dialog trigger ("…:prompt") is resolved into a concrete action before anything is saved,
    // so it is offered but never persistable — that is by design, not a defect.
    private static bool IsDialogTrigger(string action)
    {
        return action.EndsWith(":prompt", StringComparison.OrdinalIgnoreCase);
    }

    private static void AuditChoices(string source, List<Choice> choices)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Choice choice in choices)
        {
            string action = (choice.Action ?? "").Trim();
            Check(action.Length > 0, source + ": a choice has no action (" + choice.Label + ")");
            Check((choice.Label ?? "").Trim().Length > 0, source + ": action '" + action + "' has no label");
            if (action.Length == 0) continue;

            // Two entries with the same action would give the user two identical outcomes under
            // different names, and the picker would still show the first one as selected.
            string previousLabel;
            if (seen.TryGetValue(action, out previousLabel))
                Check(false, source + ": duplicate action '" + action + "' offered as both '" +
                    previousLabel + "' and '" + choice.Label + "'");
            else
                seen[action] = choice.Label;

            Check(Text(action).Length > 0, source + ": action '" + action + "' renders no label");
            // The record key is welded to the stable voice chain; it must never be offered here.
            string lowered = action.ToLowerInvariant();
            Check(lowered != "f5" && lowered != "shortcut:f5" && lowered != "voice",
                source + ": action '" + action + "' would capture the record key");

            if (IsDialogTrigger(action) ||
                action.Equals(SnippetManageAction, StringComparison.OrdinalIgnoreCase))
            {
                Check(!Persistable(action), source + ": dialog trigger '" + action + "' is treated as persistable");
                continue;
            }
            if (action.StartsWith("snippet:", StringComparison.OrdinalIgnoreCase))
            {
                Check(Supported(action), source + ": snippet action '" + action + "' is not supported");
                continue;
            }
            Check(Supported(action), source + ": action '" + action + "' is offered but not supported, " +
                "so saving it silently stores that key's default instead");
        }
    }

    // The decisive reliability property: picking an action and saving it must store that action.
    private static void AuditSilentRewrite(List<string> actions, string source)
    {
        foreach (string action in actions)
        {
            if (!Persistable(action)) continue;
            foreach (string key in ProfileKeys)
            {
                var mappings = new Dictionary<string, string>();
                foreach (string partner in WithMirror(key, action)) mappings[partner] = action;
                var normalized = Call("NormalizeShortcutProfileMappings", mappings) as Dictionary<string, string>;
                if (normalized == null) { Check(false, source + ": normalization returned nothing"); return; }
                string stored = normalized.ContainsKey(key) ? normalized[key] : "";
                string expected = Normalized(key, action);
                Check(string.Equals(stored, expected, StringComparison.OrdinalIgnoreCase),
                    source + ": '" + action + "' on " + key + " was stored as '" + stored + "'" +
                    " (expected '" + expected + "') — the picker would show one thing and do another");
                Check(!string.Equals(stored, "none", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "none", StringComparison.OrdinalIgnoreCase),
                    source + ": '" + action + "' on " + key + "' was silently disabled");
            }
        }
    }

    // ShortcutProfileConfig is a private nested type exposing properties, so it is driven by
    // reflection; Choice is a field-based nested type, so both are tried.
    private static object Raw(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name);
        if (property != null) return property.GetValue(instance, null);
        FieldInfo field = type.GetField(name);
        return field == null ? null : field.GetValue(instance);
    }

    private static string Field(object instance, string name)
    {
        return Convert.ToString(Raw(instance, name));
    }

    private static Dictionary<string, string> MappingField(object instance, string name)
    {
        return Raw(instance, name) as Dictionary<string, string>;
    }

    private static string[] NamesField(object instance, string name)
    {
        return (Raw(instance, name) as string[]) ?? new string[0];
    }

    private static void AuditStarterProfiles()
    {
        var profiles = Call("DefaultShortcutProfiles") as Array;
        Check(profiles != null && profiles.Length == ExpectedStarterIds.Length,
            "The default scheme no longer ships exactly " + ExpectedStarterIds.Length + " starter profiles");
        if (profiles == null) return;

        var ids = new List<string>();
        var nameByProcess = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (object profile in profiles)
        {
            string id = Field(profile, "id");
            string name = Field(profile, "name");
            string preset = Field(profile, "preset");
            Dictionary<string, string> mappings = MappingField(profile, "mappings");
            ids.Add(id);
            Check(name.Trim().Length > 0, "Starter profile '" + id + "' has no name");
            Check(string.Equals(preset, id, StringComparison.OrdinalIgnoreCase),
                "Starter profile '" + id + "' does not carry its own preset");

            var normalized = Call("NormalizeShortcutProfileMappings", mappings) as Dictionary<string, string>;
            Check(normalized != null, "Starter profile '" + id + "' has no mappings");
            foreach (string key in ProfileKeys)
            {
                Check(mappings != null && mappings.ContainsKey(key),
                    "Starter profile '" + id + "' is missing the mapping key " + key);
                string action = mappings != null && mappings.ContainsKey(key) ? mappings[key] : "";
                Check(Supported(action), "Starter profile '" + id + "' uses unsupported action '" +
                    action + "' on " + key);
                string after = normalized != null && normalized.ContainsKey(key) ? normalized[key] : "";
                Check(string.Equals(after, action, StringComparison.OrdinalIgnoreCase),
                    "Starter profile '" + id + "' key " + key + " changes from '" + action +
                    "' to '" + after + "' when normalized");
            }

            string[] names = NamesField(profile, "processNames");
            if (string.Equals(id, "general", StringComparison.OrdinalIgnoreCase))
                Check(names.Length == 0, "The general starter profile must not claim any application");
            else
                Check(names.Length > 0, "Starter profile '" + id + "' claims no application, " +
                    "so Smart Profiles can never select it");
            foreach (string processName in names)
            {
                Check(!string.IsNullOrWhiteSpace(processName),
                    "Starter profile '" + id + "' has an empty process name");
                string owner;
                if (nameByProcess.TryGetValue(processName ?? "", out owner))
                    Check(false, "Process '" + processName + "' is claimed by both '" + owner + "' and '" + id + "'");
                else
                    nameByProcess[processName ?? ""] = id;
            }
        }
        for (int index = 0; index < ExpectedStarterIds.Length; index++)
            Check(index < ids.Count && ids[index] == ExpectedStarterIds[index],
                "The starter profile order changed at index " + index + " (expected " + ExpectedStarterIds[index] + ")");
    }

    private static int Main()
    {
        Console.WriteLine("Auditing every shortcut action the pickers can offer...");

        List<Choice> block = Choices("CustomActionChoices", "");
        AuditChoices("action picker", block);
        Console.WriteLine("  action picker choices: " + block.Count);

        var perKey = new List<Choice>();
        foreach (string key in ProfileKeys)
        {
            // Each key legitimately offers the same list, so duplicates are judged per key.
            List<Choice> forKey = Choices("ShortcutChoicesFor", key, "");
            AuditChoices("per-key picker [" + key + "]", forKey);
            perKey.AddRange(forKey);
        }
        Console.WriteLine("  per-key choices: " + perKey.Count + " across " + ProfileKeys.Length + " keys");

        var actions = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Choice choice in block) if (seen.Add(choice.Action ?? "")) actions.Add(choice.Action ?? "");
        foreach (Choice choice in perKey) if (seen.Add(choice.Action ?? "")) actions.Add(choice.Action ?? "");
        Console.WriteLine("  distinct actions: " + actions.Count);
        AuditSilentRewrite(actions, "save round-trip");

        // The one documented rewrite, asserted so it cannot grow into a general habit.
        Check(string.Equals(Normalized("左键", "alt+left"), "browserback", StringComparison.OrdinalIgnoreCase),
            "The legacy 左键 alt+left migration is gone");
        int rewrites = 0;
        foreach (string action in actions)
            foreach (string key in ProfileKeys)
                if (!string.Equals(Normalized(key, action), action, StringComparison.OrdinalIgnoreCase)) rewrites++;
        Check(rewrites <= 1, "Normalization rewrites " + rewrites + " actions; only 左键/alt+left may be rewritten");

        AuditStarterProfiles();

        if (failures > 0)
        {
            Console.Error.WriteLine("Shortcut action audit failed with " + failures + " problem(s).");
            return 1;
        }
        Console.WriteLine("Shortcut action audit passed: every offered action is supported, labelled, and stored as chosen.");
        return 0;
    }
}
