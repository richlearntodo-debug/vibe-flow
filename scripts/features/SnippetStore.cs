using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// User-authored text snippets ("phrase packs"): the user's own short phrases that a remote key or a
// gesture layer can type straight into whatever holds focus.
//
// Privacy boundary, deliberately narrow. This store only ever holds text the user typed themselves. It
// never receives transcription output and never reads it back, nothing here is uploaded, logged, or sent
// anywhere, and injection happens per character with Unicode key events at dispatch time so a snippet
// never touches the clipboard and cannot be captured by a clipboard manager or replayed into the wrong
// window by a paste race.
//
// This file is deliberately ASCII-only. It is written without a byte-order mark, and the C# compiler
// falls back to the ANSI code page for BOM-less sources, so a non-ASCII literal here would be mangled.
// All user-facing text is formatted by the Host instead.
internal sealed class Snippet
{
    public string id { get; set; }
    public string name { get; set; }
    public string text { get; set; }
}

internal sealed class SnippetDocument
{
    public int schemaVersion { get; set; }
    public List<Snippet> snippets { get; set; }
}

// The shared action-label helpers (CustomActionText / MappingCardActionText) are static and are pinned by
// the source gates, so they cannot reach Host instance state. The Host therefore publishes two read-only
// lookups here once at startup. Both stay null in headless tools, and every caller tolerates that.
internal static class SnippetNaming
{
    internal static Func<string, string> ResolveName;
    internal static Func<IList<Snippet>> ResolveAll;

    internal static string NameOf(string id)
    {
        if (ResolveName == null || string.IsNullOrEmpty(id)) return "";
        try { return ResolveName(id) ?? ""; }
        catch { return ""; }
    }

    internal static IList<Snippet> All()
    {
        if (ResolveAll == null) return new List<Snippet>();
        try { return ResolveAll() ?? new List<Snippet>(); }
        catch { return new List<Snippet>(); }
    }
}

internal sealed class SnippetStore
{
    internal const int SchemaVersion = 1;
    // A snippet is typed into a live text field, so it stays short enough for the user to review.
    internal const int MaxTextLength = 500;
    internal const int MaxNameLength = 40;
    internal const int MaxSnippets = 40;
    internal const string ActionPrefix = "snippet:";
    // The picker entry that opens the manager instead of binding an action.
    internal const string ManageAction = "snippet:manage";
    internal const string TextEmptyCode = "SNIPPET-TEXT-EMPTY";
    internal const string TextTooLongCode = "SNIPPET-TEXT-TOO-LONG";
    internal const string NoNameCode = "SNIPPET-NO-NAME";
    internal const string NameTooLongCode = "SNIPPET-NAME-TOO-LONG";
    internal const string TooManyCode = "SNIPPET-TOO-MANY";

    private readonly string path;
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    internal SnippetStore(string userStateRoot)
    {
        path = Path.Combine(userStateRoot, "snippets.json");
    }

    internal string FilePath { get { return path; } }

    internal SnippetDocument Load()
    {
        try
        {
            if (!File.Exists(path)) return Empty();
            SnippetDocument document = serializer.Deserialize<SnippetDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.schemaVersion > SchemaVersion) return Empty();
            if (document.snippets == null) document.snippets = new List<Snippet>();
            return document;
        }
        catch
        {
            return Empty();
        }
    }

    internal bool TrySave(SnippetDocument document)
    {
        if (document == null) return false;
        try
        {
            document.schemaVersion = SchemaVersion;
            if (document.snippets == null) document.snippets = new List<Snippet>();
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

    private static SnippetDocument Empty()
    {
        return new SnippetDocument { schemaVersion = SchemaVersion, snippets = new List<Snippet>() };
    }

    internal static string ActionFor(string id)
    {
        return ActionPrefix + (id ?? "").Trim();
    }

    internal static string IdOf(string action)
    {
        string value = (action ?? "").Trim();
        if (!value.StartsWith(ActionPrefix, StringComparison.OrdinalIgnoreCase)) return "";
        return value.Substring(ActionPrefix.Length).Trim();
    }

    // True for a bindable snippet action; the manager entry is explicitly not one.
    internal static bool IsSnippetAction(string action)
    {
        string id = IdOf(action);
        return id.Length > 0 && !string.Equals(id, "manage", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsManageAction(string action)
    {
        return string.Equals((action ?? "").Trim(), ManageAction, StringComparison.OrdinalIgnoreCase);
    }

    // Newlines are preserved (a snippet may be several lines); surrounding whitespace is not.
    internal static string NormalizeText(string text)
    {
        return (text ?? "").Replace("\r\n", "\n").Trim();
    }

    internal static Snippet Find(SnippetDocument document, string id)
    {
        if (document == null || document.snippets == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (Snippet snippet in document.snippets)
        {
            if (snippet != null && string.Equals(snippet.id, id, StringComparison.OrdinalIgnoreCase)) return snippet;
        }
        return null;
    }

    internal static string Describe(SnippetDocument document, string action)
    {
        Snippet snippet = Find(document, IdOf(action));
        return snippet == null ? "" : (snippet.name ?? "");
    }

    // Returns "" when the snippet may be saved, otherwise a reason code.
    internal static string Validate(string name, string text, SnippetDocument document)
    {
        string normalizedText = NormalizeText(text);
        if (normalizedText.Length == 0) return TextEmptyCode;
        if (normalizedText.Length > MaxTextLength) return TextTooLongCode;
        string trimmedName = (name ?? "").Trim();
        if (trimmedName.Length == 0) return NoNameCode;
        if (trimmedName.Length > MaxNameLength) return NameTooLongCode;
        if (document != null && document.snippets != null && document.snippets.Count >= MaxSnippets) return TooManyCode;
        return "";
    }

    // Adds a snippet (empty id) or updates an existing one, returning it.
    internal static Snippet Upsert(SnippetDocument document, string id, string name, string text)
    {
        if (document == null) return null;
        if (document.snippets == null) document.snippets = new List<Snippet>();
        Snippet snippet = Find(document, id);
        if (snippet == null)
        {
            snippet = new Snippet { id = NewId(document) };
            document.snippets.Add(snippet);
        }
        snippet.name = (name ?? "").Trim();
        snippet.text = NormalizeText(text);
        return snippet;
    }

    internal static bool Remove(SnippetDocument document, string id)
    {
        Snippet snippet = Find(document, id);
        if (snippet == null) return false;
        document.snippets.Remove(snippet);
        return true;
    }

    private static string NewId(SnippetDocument document)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            string candidate = "snip-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            if (Find(document, candidate) == null) return candidate;
        }
        return "snip-" + Guid.NewGuid().ToString("N");
    }
}
