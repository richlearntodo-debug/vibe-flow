using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class NoteItem
{
    internal Dictionary<string, object> UnknownFields { get; set; }
    public string Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long Revision { get; set; }
    public bool IsDeleted { get; set; }
    public List<NoteAiResult> AiResults { get; set; }

    internal NoteItem()
    {
        Id = Guid.NewGuid().ToString("N");
        Title = "";
        Body = "";
        Category = "收件箱";
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        Revision = 1;
        AiResults = new List<NoteAiResult>();
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal NoteItem Copy()
    {
        return new NoteItem
        {
            Id = Id,
            Title = Title,
            Body = Body,
            Category = Category,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
            Revision = Revision,
            IsDeleted = IsDeleted,
            AiResults = AiResults == null ? new List<NoteAiResult>() :
                AiResults.Select(result => result == null ? null : result.Copy()).ToList(),
            UnknownFields = NotesValueCopy.CopyDictionary(UnknownFields)
        };
    }

    internal string DisplayTitle()
    {
        if (!string.IsNullOrWhiteSpace(Title)) return Title.Trim();
        string firstLine = (Body ?? "").Replace("\r", "").Split('\n')[0].Trim();
        return firstLine.Length == 0 ? "未命名便签" : firstLine.Length > 48 ? firstLine.Substring(0, 48) + "…" : firstLine;
    }

    public override string ToString()
    {
        return DisplayTitle();
    }
}

internal sealed class NoteAiResult
{
    internal Dictionary<string, object> UnknownFields { get; set; }
    public string Id { get; set; }
    public string Operation { get; set; }
    public string Output { get; set; }
    public string Model { get; set; }
    public long SourceRevision { get; set; }
    public List<string> SourceNoteIds { get; set; }
    public List<long> SourceRevisions { get; set; }
    public string PromptVersion { get; set; }
    public string ProviderId { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    internal NoteAiResult()
    {
        Id = Guid.NewGuid().ToString("N");
        Operation = "organize";
        Output = "";
        Model = "";
        SourceRevision = 0;
        SourceNoteIds = new List<string>();
        SourceRevisions = new List<long>();
        PromptVersion = "";
        ProviderId = "";
        Status = "saved";
        CreatedAtUtc = DateTime.UtcNow;
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal NoteAiResult Copy()
    {
        return new NoteAiResult
        {
            Id = Id,
            Operation = Operation,
            Output = Output,
            Model = Model,
            SourceRevision = SourceRevision,
            SourceNoteIds = SourceNoteIds == null ? new List<string>() : new List<string>(SourceNoteIds),
            SourceRevisions = SourceRevisions == null ? new List<long>() : new List<long>(SourceRevisions),
            PromptVersion = PromptVersion,
            ProviderId = ProviderId,
            Status = Status,
            CreatedAtUtc = CreatedAtUtc,
            UnknownFields = NotesValueCopy.CopyDictionary(UnknownFields)
        };
    }
}

internal static class NotesAiResultPolicy
{
    internal const string StaleNoteErrorCode = "NOTES-AI-STALE-NOTE";

    internal static bool IsCurrent(NotesStore store, NoteItem source, out string errorCode)
    {
        errorCode = "";
        if (store == null || source == null || string.IsNullOrWhiteSpace(source.Id))
        {
            errorCode = StaleNoteErrorCode;
            return false;
        }
        NotesLoadResult loaded = store.Load();
        if (!loaded.IsSuccess || loaded.Document == null || loaded.Document.notes == null)
        {
            errorCode = string.IsNullOrWhiteSpace(loaded.ErrorCode) ?
                "NOTES-STORE-READ-FAILED" : loaded.ErrorCode;
            return false;
        }
        NoteItem current = loaded.Document.notes.FirstOrDefault(item => item != null &&
            string.Equals(item.Id, source.Id, StringComparison.OrdinalIgnoreCase));
        if (current == null || current.IsDeleted || current.Revision != source.Revision)
        {
            errorCode = StaleNoteErrorCode;
            return false;
        }
        return true;
    }
}

internal sealed class NotesDocument
{
    internal Dictionary<string, object> UnknownFields { get; set; }
    internal bool RecoveredFromBackup { get; set; }
    public int schemaVersion { get; set; }
    public long revision { get; set; }
    public List<NoteItem> notes { get; set; }

    internal NotesDocument()
    {
        schemaVersion = NotesStore.CurrentSchemaVersion;
        revision = 0;
        notes = new List<NoteItem>();
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        RecoveredFromBackup = false;
    }
}

internal static class NoteDraftPolicy
{
    internal static bool ShouldDiscardNewDraft(bool isNewDraft, string title, string body)
    {
        return isNewDraft && string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body);
    }

    // Keep the timer alive while recording so the first tick after release can
    // persist the pending edit without touching the voice path.
    internal static bool ShouldScheduleAutoSave(bool dirty)
    {
        return dirty;
    }

    internal static bool ShouldDeferAutoSave(bool dirty, bool recording)
    {
        return dirty && recording;
    }
}

internal static class NoteCategoryPolicy
{
    internal const string DefaultCategory = "收件箱";
    internal const int MaxLength = 48;

    internal static bool TryNormalize(string value, out string normalized, out string errorCode)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? DefaultCategory : value.Trim();
        errorCode = "";
        if (normalized.Length > MaxLength)
        {
            normalized = "";
            errorCode = "NOTES-CATEGORY-TOO-LONG";
            return false;
        }
        foreach (char character in normalized)
        {
            if (char.IsControl(character) || character == '/' || character == '\\')
            {
                normalized = "";
                errorCode = "NOTES-CATEGORY-INVALID";
                return false;
            }
        }
        return true;
    }

    internal static List<string> CollectCategories(IEnumerable<NoteItem> notes)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        categories.Add(DefaultCategory);
        if (notes != null)
        {
            foreach (NoteItem note in notes)
            {
                string normalized;
                string error;
                if (note != null && TryNormalize(note.Category, out normalized, out error))
                    categories.Add(normalized);
            }
        }
        return categories.OrderBy(category => string.Equals(category, DefaultCategory,
            StringComparison.OrdinalIgnoreCase) ? "" : category, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

internal sealed class NoteCategorySuggestion
{
    internal string NoteId { get; private set; }
    internal string ExistingCategoryId { get; private set; }
    internal string NewCategoryName { get; private set; }
    internal string Reason { get; private set; }
    internal string TargetCategory
    {
        get
        {
            return !string.IsNullOrWhiteSpace(ExistingCategoryId) ? ExistingCategoryId :
                !string.IsNullOrWhiteSpace(NewCategoryName) ? NewCategoryName : NoteCategoryPolicy.DefaultCategory;
        }
    }

    private NoteCategorySuggestion() { }

    internal static bool TryParse(string output, string expectedNoteId, IEnumerable<string> existingCategories,
        out NoteCategorySuggestion suggestion, out string errorCode)
    {
        suggestion = null;
        errorCode = "";
        Dictionary<string, object> raw = ParseObject(output);
        if (raw == null)
        {
            errorCode = "AI-CATEGORY-FORMAT";
            return false;
        }
        string noteId = ReadString(raw, "noteId").Trim();
        if (noteId.Length == 0 || !string.Equals(noteId, (expectedNoteId ?? "").Trim(), StringComparison.Ordinal))
        {
            errorCode = "AI-CATEGORY-NOTE-MISMATCH";
            return false;
        }
        string existing = ReadString(raw, "existingCategoryId").Trim();
        string next = ReadString(raw, "newCategoryName").Trim();
        if (existing.Length > 0 && next.Length > 0)
        {
            errorCode = "AI-CATEGORY-MULTIPLE-TARGETS";
            return false;
        }
        List<string> categories = NoteCategoryPolicy.CollectCategories(
            (existingCategories ?? Enumerable.Empty<string>()).Select(delegate(string category)
            {
                return new NoteItem { Category = category };
            }));
        if (existing.Length > 0)
        {
            string canonical = categories.FirstOrDefault(category =>
                string.Equals(category, existing, StringComparison.OrdinalIgnoreCase));
            if (canonical == null)
            {
                errorCode = "AI-CATEGORY-EXISTING-NOT-FOUND";
                return false;
            }
            existing = canonical;
        }
        else if (next.Length > 0)
        {
            string normalized;
            string categoryError;
            if (!NoteCategoryPolicy.TryNormalize(next, out normalized, out categoryError))
            {
                errorCode = "AI-CATEGORY-NAME-INVALID";
                return false;
            }
            if (categories.Any(category => string.Equals(category, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                errorCode = "AI-CATEGORY-NEW-ALREADY-EXISTS";
                return false;
            }
            next = normalized;
        }
        string reason = ReadString(raw, "reason").Trim();
        if (reason.Length > 240) reason = reason.Substring(0, 240);
        suggestion = new NoteCategorySuggestion
        {
            NoteId = noteId,
            ExistingCategoryId = existing,
            NewCategoryName = next,
            Reason = reason
        };
        return true;
    }

    private static Dictionary<string, object> ParseObject(string output)
    {
        string text = (output ?? "").Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLine = text.IndexOf('\n');
            int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLine >= 0 && lastFence > firstLine)
                text = text.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
        }
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            return new JavaScriptSerializer().DeserializeObject(text.Substring(start, end - start + 1))
                as Dictionary<string, object>;
        }
        catch { return null; }
    }

    private static string ReadString(Dictionary<string, object> raw, string key)
    {
        object value;
        return raw != null && raw.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
    }
}

internal static class NotesSearchPolicy
{
    internal static bool Matches(NoteItem note, string query)
    {
        if (note == null) return false;
        string normalized = (query ?? "").Trim();
        if (normalized.Length == 0) return true;
        return (note.Title ?? "").IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (note.Body ?? "").IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (note.Category ?? "").IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

internal static class NotesValueCopy
{
    internal static Dictionary<string, object> CopyDictionary(Dictionary<string, object> source)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (source == null) return copy;
        foreach (KeyValuePair<string, object> pair in source) copy[pair.Key] = CopyValue(pair.Value);
        return copy;
    }

    internal static object CopyValue(object value)
    {
        Dictionary<string, object> dictionary = value as Dictionary<string, object>;
        if (dictionary != null) return CopyDictionary(dictionary);
        object[] array = value as object[];
        if (array != null)
        {
            var result = new object[array.Length];
            for (int index = 0; index < array.Length; index++) result[index] = CopyValue(array[index]);
            return result;
        }
        System.Collections.ArrayList list = value as System.Collections.ArrayList;
        if (list != null)
        {
            var result = new object[list.Count];
            for (int index = 0; index < list.Count; index++) result[index] = CopyValue(list[index]);
            return result;
        }
        return value;
    }
}

internal sealed class NotesLoadResult
{
    internal bool IsSuccess { get; private set; }
    internal bool RecoveredFromBackup { get; private set; }
    internal string ErrorCode { get; private set; }
    internal NotesDocument Document { get; private set; }

    private NotesLoadResult() { }

    internal static NotesLoadResult Success(NotesDocument document, bool recovered)
    {
        return new NotesLoadResult
        {
            IsSuccess = true,
            RecoveredFromBackup = recovered,
            ErrorCode = "",
            Document = document ?? new NotesDocument()
        };
    }

    internal static NotesLoadResult Failure(string errorCode)
    {
        return new NotesLoadResult
        {
            IsSuccess = false,
            RecoveredFromBackup = false,
            ErrorCode = errorCode ?? "NOTES-STORE-READ-FAILED",
            Document = new NotesDocument()
        };
    }
}
