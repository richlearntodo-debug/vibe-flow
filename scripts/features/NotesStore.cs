using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Web.Script.Serialization;

internal sealed class NotesStore
{
    internal const int CurrentSchemaVersion = 1;
    private static readonly HashSet<string> RootFields = new HashSet<string>(
        new[] { "schemaVersion", "revision", "notes" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NoteFields = new HashSet<string>(
        new[] { "Id", "Title", "Body", "Category", "CreatedAtUtc", "UpdatedAtUtc", "Revision", "IsDeleted", "AiResults" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AiResultFields = new HashSet<string>(
        new[] { "Id", "Operation", "Output", "Model", "SourceRevision", "SourceNoteIds", "SourceRevisions", "PromptVersion", "ProviderId", "Status", "CreatedAtUtc" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly object StoreLocksGuard = new object();
    private static readonly Dictionary<string, object> StoreLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private readonly string path;
    private readonly string backupPath;
    private readonly object storeLock;

    internal NotesStore(string userStateRoot)
    {
        Directory.CreateDirectory(userStateRoot);
        path = Path.Combine(userStateRoot, "notes.json");
        backupPath = path + ".bak";
        lock (StoreLocksGuard)
        {
            if (!StoreLocks.TryGetValue(path, out storeLock))
            {
                storeLock = new object();
                StoreLocks[path] = storeLock;
            }
        }
    }

    internal string PathOnDisk { get { return path; } }

    internal NotesLoadResult Load()
    {
        lock (storeLock)
        {
            NotesDocument document;
            if (TryRead(path, out document)) return NotesLoadResult.Success(Normalize(document), false);
            if (TryRead(backupPath, out document))
            {
                document.RecoveredFromBackup = true;
                return NotesLoadResult.Success(Normalize(document), true);
            }
            if (!File.Exists(path) && !File.Exists(backupPath)) return NotesLoadResult.Success(new NotesDocument(), false);
            return NotesLoadResult.Failure("NOTES-STORE-CORRUPT");
        }
    }

    internal bool TrySave(NotesDocument document, out string errorCode)
    {
        lock (storeLock)
        {
            errorCode = "";
            try
            {
                NotesLoadResult current = Load();
                bool hasCurrentDocument = File.Exists(path) || File.Exists(backupPath);
                if (!current.IsSuccess && hasCurrentDocument)
                {
                    errorCode = current.ErrorCode.Length == 0 ? "NOTES-STORE-READ-FAILED" : current.ErrorCode;
                    return false;
                }
                long expectedRevision = document == null ? 0 : Math.Max(0, document.revision);
                long currentRevision = current.IsSuccess && current.Document != null
                    ? Math.Max(0, current.Document.revision) : 0;
                if (expectedRevision != currentRevision)
                {
                    errorCode = "NOTES-REVISION-CONFLICT";
                    return false;
                }
                NotesDocument safe = Normalize(document);
                safe.revision = currentRevision + 1;
                string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                string json = SerializeDocument(safe);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(tempPath, path, safe.RecoveredFromBackup ? null : backupPath, true);
                else
                {
                    File.Move(tempPath, path);
                    if (!File.Exists(backupPath)) File.Copy(path, backupPath, true);
                }
                document.schemaVersion = safe.schemaVersion;
                document.revision = safe.revision;
                document.notes = safe.notes;
                document.UnknownFields = NotesValueCopy.CopyDictionary(safe.UnknownFields);
                document.RecoveredFromBackup = false;
                return true;
            }
            catch
            {
                errorCode = "NOTES-STORE-WRITE-FAILED";
                return false;
            }
        }
    }

    // Re-read the latest document and compare the note revision before applying
    // a mutation. This is the conflict boundary shared by the main editor and
    // the modeless Deck.
    internal bool TryUpdate(string noteId, long expectedRevision, Func<NoteItem, bool> mutation,
        out string errorCode)
    {
        lock (storeLock)
        {
        errorCode = "";
        if (string.IsNullOrWhiteSpace(noteId) || mutation == null)
        {
            errorCode = "NOTES-INVALID-UPDATE";
            return false;
        }
        NotesLoadResult loaded = Load();
        if (!loaded.IsSuccess || loaded.Document == null || loaded.Document.notes == null)
        {
            errorCode = loaded.ErrorCode.Length == 0 ? "NOTES-STORE-READ-FAILED" : loaded.ErrorCode;
            return false;
        }
        NoteItem note = loaded.Document.notes.FirstOrDefault(item => item != null &&
            string.Equals(item.Id, noteId, StringComparison.OrdinalIgnoreCase));
        if (note == null)
        {
            errorCode = "NOTES-NOT-FOUND";
            return false;
        }
        if (note.Revision != expectedRevision)
        {
            errorCode = "NOTES-REVISION-CONFLICT";
            return false;
        }
        bool changed;
        try { changed = mutation(note); }
        catch
        {
            errorCode = "NOTES-UPDATE-FAILED";
            return false;
        }
        if (!changed)
        {
            errorCode = "NOTES-NO-CHANGE";
            return false;
        }
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        return TrySave(loaded.Document, out errorCode);
        }
    }

    // Remove a newly-created draft only when the caller still owns the note
    // revision. This prevents Cancel from deleting another window's edit.
    internal bool TryDiscardDraft(string noteId, long expectedRevision, out string errorCode)
    {
        lock (storeLock)
        {
            errorCode = "";
            if (string.IsNullOrWhiteSpace(noteId))
            {
                errorCode = "NOTES-NOT-FOUND";
                return false;
            }
            NotesLoadResult loaded = Load();
            if (!loaded.IsSuccess || loaded.Document == null || loaded.Document.notes == null)
            {
                errorCode = loaded.ErrorCode.Length == 0 ? "NOTES-STORE-READ-FAILED" : loaded.ErrorCode;
                return false;
            }
            NoteItem note = loaded.Document.notes.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, noteId, StringComparison.OrdinalIgnoreCase));
            if (note == null)
            {
                errorCode = "NOTES-NOT-FOUND";
                return false;
            }
            if (note.Revision != expectedRevision)
            {
                errorCode = "NOTES-REVISION-CONFLICT";
                return false;
            }
            loaded.Document.notes.Remove(note);
            return TrySave(loaded.Document, out errorCode);
        }
    }

    internal bool TryDelete(NotesDocument document, string id, out string errorCode)
    {
        return TrySetDeleted(document, id, true, out errorCode);
    }

    internal bool TryRestore(NotesDocument document, string id, out string errorCode)
    {
        return TrySetDeleted(document, id, false, out errorCode);
    }

    internal List<NoteItem> Search(NotesDocument document, string query, bool includeDeleted)
    {
        string normalized = (query ?? "").Trim();
        IEnumerable<NoteItem> source = (document == null || document.notes == null)
            ? Enumerable.Empty<NoteItem>() : document.notes;
        if (!includeDeleted) source = source.Where(note => note != null && !note.IsDeleted);
        return source.Where(note => NotesSearchPolicy.Matches(note, normalized))
            .Select(note => note.Copy()).ToList();
    }

    internal bool Export(IEnumerable<NoteItem> selected, string destination, bool markdown, out string errorCode)
    {
        errorCode = "";
        string safeDestination;
        if (!TryValidateUserPath(destination, false, out safeDestination, out errorCode)) return false;
        try
        {
            IEnumerable<NoteItem> notes = selected ?? Enumerable.Empty<NoteItem>();
            StringBuilder output = new StringBuilder();
            foreach (NoteItem note in notes)
            {
                if (note == null || note.IsDeleted) continue;
                if (markdown)
                {
                    output.Append("# ").AppendLine(note.DisplayTitle());
                    output.Append("分类：").AppendLine(note.Category ?? "收件箱");
                    output.Append("更新时间：").AppendLine(note.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                    output.AppendLine().AppendLine(note.Body ?? "").AppendLine().AppendLine("---").AppendLine();
                }
                else
                {
                    output.AppendLine(note.DisplayTitle());
                    output.AppendLine(note.Body ?? "");
                    output.AppendLine();
                }
            }
            return TryWriteExternalFile(safeDestination, output.ToString(), out errorCode);
        }
        catch
        {
            errorCode = "NOTES-EXPORT-FAILED";
            return false;
        }
    }

    internal bool TryExportJsonBackup(string destination, out string errorCode)
    {
        lock (storeLock)
        {
            errorCode = "";
            string safeDestination;
            if (!TryValidateUserPath(destination, true, out safeDestination, out errorCode)) return false;
            NotesLoadResult loaded = Load();
            if (!loaded.IsSuccess)
            {
                errorCode = loaded.ErrorCode.Length == 0 ? "NOTES-STORE-READ-FAILED" : loaded.ErrorCode;
                return false;
            }
            try
            {
                return TryWriteExternalFile(safeDestination, SerializeDocument(loaded.Document), out errorCode);
            }
            catch
            {
                errorCode = "NOTES-BACKUP-WRITE-FAILED";
                return false;
            }
        }
    }

    internal bool TryRestoreJsonBackup(string source, out string errorCode)
    {
        lock (storeLock)
        {
            errorCode = "";
            string safeSource;
            if (!TryValidateUserPath(source, true, out safeSource, out errorCode)) return false;
            if (!File.Exists(safeSource))
            {
                errorCode = "NOTES-BACKUP-NOT-FOUND";
                return false;
            }
            if (!TryValidateBackupShape(safeSource, out errorCode)) return false;
            NotesDocument imported;
            if (!TryRead(safeSource, out imported) || !ValidateImportedDocument(imported, out errorCode))
            {
                if (errorCode.Length == 0) errorCode = "NOTES-BACKUP-INVALID";
                return false;
            }
            NotesLoadResult current = Load();
            bool hasCurrentDocument = File.Exists(path) || File.Exists(backupPath);
            if (!current.IsSuccess && hasCurrentDocument)
            {
                errorCode = current.ErrorCode.Length == 0 ? "NOTES-STORE-READ-FAILED" : current.ErrorCode;
                return false;
            }
            imported = Normalize(imported);
            imported.revision = current.IsSuccess && current.Document != null
                ? Math.Max(0, current.Document.revision) : 0;
            // If the live store was itself recovered from notes.json.bak, do not
            // replace that valid backup with the corrupt primary file while
            // applying an external restore.
            imported.RecoveredFromBackup = current.IsSuccess && current.RecoveredFromBackup;
            return TrySave(imported, out errorCode);
        }
    }

    private bool TrySetDeleted(NotesDocument document, string id, bool deleted, out string errorCode)
    {
        errorCode = "";
        if (document == null || document.notes == null || string.IsNullOrWhiteSpace(id))
        {
            errorCode = "NOTES-NOT-FOUND";
            return false;
        }
        NoteItem note = document.notes.FirstOrDefault(item => item != null &&
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (note == null)
        {
            errorCode = "NOTES-NOT-FOUND";
            return false;
        }
        note.IsDeleted = deleted;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        return TrySave(document, out errorCode);
    }

    private static NotesDocument Normalize(NotesDocument document)
    {
        NotesDocument normalized = document ?? new NotesDocument();
        normalized.schemaVersion = CurrentSchemaVersion;
        if (normalized.UnknownFields == null) normalized.UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (normalized.notes == null) normalized.notes = new List<NoteItem>();
        normalized.notes = normalized.notes.Where(note => note != null).Select(note =>
        {
            if (string.IsNullOrWhiteSpace(note.Id)) note.Id = Guid.NewGuid().ToString("N");
            if (note.CreatedAtUtc == default(DateTime)) note.CreatedAtUtc = DateTime.UtcNow;
            if (note.UpdatedAtUtc == default(DateTime)) note.UpdatedAtUtc = note.CreatedAtUtc;
            string normalizedCategory;
            string categoryError;
            if (!NoteCategoryPolicy.TryNormalize(note.Category, out normalizedCategory, out categoryError))
                normalizedCategory = NoteCategoryPolicy.DefaultCategory;
            note.Category = normalizedCategory;
            if (note.Title == null) note.Title = "";
            if (note.Body == null) note.Body = "";
            if (note.AiResults == null) note.AiResults = new List<NoteAiResult>();
            if (note.Revision < 1) note.Revision = 1;
            if (note.UnknownFields == null) note.UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return note;
        }).ToList();
        return normalized;
    }

    private bool TryValidateUserPath(string candidate, bool json, out string fullPath, out string errorCode)
    {
        fullPath = "";
        errorCode = "";
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathRooted(candidate.Trim()) ||
            IsDriveRelativePath(candidate.Trim()) || ContainsParentTraversal(candidate))
        {
            errorCode = "NOTES-PATH-UNSAFE";
            return false;
        }
        try { fullPath = Path.GetFullPath(candidate.Trim()); }
        catch
        {
            errorCode = "NOTES-PATH-UNSAFE";
            return false;
        }
        if (string.Equals(fullPath, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, backupPath, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(fullPath))
        {
            errorCode = "NOTES-PATH-UNSAFE";
            return false;
        }
        string fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errorCode = "NOTES-PATH-UNSAFE";
            return false;
        }
        if (json && !string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "NOTES-BACKUP-EXTENSION";
            return false;
        }
        return true;
    }

    private static bool TryValidateBackupShape(string source, out string errorCode)
    {
        errorCode = "";
        try
        {
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(source, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null)
            {
                errorCode = "NOTES-BACKUP-FORMAT";
                return false;
            }
            object rawNotes;
            if (!raw.TryGetValue("notes", out rawNotes) || !(rawNotes is object[]))
            {
                errorCode = "NOTES-BACKUP-FORMAT";
                return false;
            }
            int schema;
            if (!TryReadSchemaVersion(raw, out schema) || schema < 0)
            {
                errorCode = "NOTES-BACKUP-FORMAT";
                return false;
            }
            if (schema > CurrentSchemaVersion)
            {
                errorCode = "NOTES-BACKUP-SCHEMA-UNSUPPORTED";
                return false;
            }
            return true;
        }
        catch
        {
            errorCode = "NOTES-BACKUP-FORMAT";
            return false;
        }
    }

    private static bool ContainsParentTraversal(string candidate)
    {
        string normalized = (candidate ?? "").Replace('/', '\\');
        foreach (string part in normalized.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            if (part == "..") return true;
        return false;
    }

    private static bool IsDriveRelativePath(string candidate)
    {
        return candidate != null && candidate.Length >= 2 &&
            ((candidate[0] >= 'A' && candidate[0] <= 'Z') || (candidate[0] >= 'a' && candidate[0] <= 'z')) &&
            candidate[1] == ':' && (candidate.Length == 2 || (candidate[2] != '\\' && candidate[2] != '/'));
    }

    private static bool TryWriteExternalFile(string destination, string content, out string errorCode)
    {
        errorCode = "";
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            string parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            {
                errorCode = "NOTES-EXPORT-DIRECTORY-MISSING";
                return false;
            }
            File.WriteAllText(temporary, content ?? "", new UTF8Encoding(false));
            if (File.Exists(destination)) File.Replace(temporary, destination, null, true);
            else File.Move(temporary, destination);
            if (!File.Exists(destination))
            {
                errorCode = "NOTES-EXPORT-WRITE-FAILED";
                return false;
            }
            return true;
        }
        catch
        {
            errorCode = "NOTES-EXPORT-WRITE-FAILED";
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            return false;
        }
    }

    private static bool ValidateImportedDocument(NotesDocument document, out string errorCode)
    {
        errorCode = "";
        if (document == null || document.schemaVersion > CurrentSchemaVersion || document.notes == null)
        {
            errorCode = "NOTES-BACKUP-SCHEMA-UNSUPPORTED";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NoteItem note in document.notes)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.Id) || !ids.Add(note.Id))
            {
                errorCode = "NOTES-BACKUP-NOTE-ID-INVALID";
                return false;
            }
        }
        return true;
    }

    private static bool TryRead(string source, out NotesDocument document)
    {
        document = null;
        try
        {
            if (!File.Exists(source)) return false;
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(source, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;
            document = new NotesDocument();
            int schema;
            if (!TryReadSchemaVersion(raw, out schema) || schema < 0) return false;
            document.schemaVersion = schema;
            document.revision = ReadLong(raw, "revision", 0);
            document.UnknownFields = CopyUnknownFields(raw, RootFields);
            object rawNotes;
            if (!raw.TryGetValue("notes", out rawNotes) || !(rawNotes is object[])) return false;
            foreach (object value in (object[])rawNotes)
            {
                Dictionary<string, object> rawNote = value as Dictionary<string, object>;
                if (rawNote == null || !IsValidAiResultsArray(rawNote)) return false;
                NoteItem note = new NoteItem
                {
                    Id = ReadString(rawNote, "Id"),
                    Title = ReadString(rawNote, "Title"),
                    Body = ReadString(rawNote, "Body"),
                    Category = ReadString(rawNote, "Category"),
                    Revision = ReadLong(rawNote, "Revision", 1),
                    IsDeleted = ReadBool(rawNote, "IsDeleted", false),
                    CreatedAtUtc = ReadUtc(rawNote, "CreatedAtUtc"),
                    UpdatedAtUtc = ReadUtc(rawNote, "UpdatedAtUtc")
                };
                note.AiResults = ReadAiResults(rawNote);
                note.UnknownFields = CopyUnknownFields(rawNote, NoteFields);
                document.notes.Add(note);
            }
            return document.schemaVersion <= CurrentSchemaVersion;
        }
        catch { return false; }
    }

    private static string SerializeDocument(NotesDocument document)
    {
        var raw = NotesValueCopy.CopyDictionary(document.UnknownFields);
        raw["schemaVersion"] = document.schemaVersion;
        raw["revision"] = document.revision;
        var notes = new List<Dictionary<string, object>>();
        foreach (NoteItem note in document.notes)
        {
            var rawNote = NotesValueCopy.CopyDictionary(note.UnknownFields);
            rawNote["Id"] = note.Id; rawNote["Title"] = note.Title; rawNote["Body"] = note.Body;
            rawNote["Category"] = note.Category;
            rawNote["CreatedAtUtc"] = note.CreatedAtUtc.ToUniversalTime().ToString("o");
            rawNote["UpdatedAtUtc"] = note.UpdatedAtUtc.ToUniversalTime().ToString("o");
            rawNote["Revision"] = note.Revision; rawNote["IsDeleted"] = note.IsDeleted;
            rawNote["AiResults"] = SerializeAiResults(note.AiResults);
            notes.Add(rawNote);
        }
        raw["notes"] = notes;
        return new JavaScriptSerializer().Serialize(raw);
    }

    private static string ReadString(Dictionary<string, object> raw, string key)
    {
        object value;
        return raw != null && raw.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
    }

    private static bool TryReadSchemaVersion(Dictionary<string, object> raw, out int schema)
    {
        schema = 0;
        object value;
        return raw != null && raw.TryGetValue("schemaVersion", out value) && value != null &&
            Int32.TryParse(Convert.ToString(value), out schema);
    }

    private static int ReadInt(Dictionary<string, object> raw, string key, int fallback)
    {
        object value;
        int parsed;
        return raw != null && raw.TryGetValue(key, out value) && value != null && Int32.TryParse(Convert.ToString(value), out parsed)
            ? parsed : fallback;
    }

    private static long ReadLong(Dictionary<string, object> raw, string key, long fallback)
    {
        object value;
        long parsed;
        return raw != null && raw.TryGetValue(key, out value) && value != null && Int64.TryParse(Convert.ToString(value), out parsed)
            ? parsed : fallback;
    }

    private static List<string> ReadStringList(Dictionary<string, object> raw, string key)
    {
        object value;
        object[] values;
        if (raw == null || !raw.TryGetValue(key, out value) || (values = value as object[]) == null)
            return new List<string>();
        return values.Where(item => item != null).Select(Convert.ToString)
            .Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
    }

    private static List<long> ReadLongList(Dictionary<string, object> raw, string key)
    {
        object value;
        object[] values;
        if (raw == null || !raw.TryGetValue(key, out value) || (values = value as object[]) == null)
            return new List<long>();
        var result = new List<long>();
        foreach (object item in values)
        {
            long parsed;
            if (item != null && Int64.TryParse(Convert.ToString(item), out parsed)) result.Add(parsed);
        }
        return result;
    }

    private static bool ReadBool(Dictionary<string, object> raw, string key, bool fallback)
    {
        object value;
        bool parsed;
        return raw != null && raw.TryGetValue(key, out value) && value != null && Boolean.TryParse(Convert.ToString(value), out parsed)
            ? parsed : fallback;
    }

    private static DateTime ReadUtc(Dictionary<string, object> raw, string key)
    {
        string value = ReadString(raw, key);
        DateTime parsed;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            return parsed.ToUniversalTime();
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(value ?? "", @"/Date\(([-0-9]+)\)/");
        long milliseconds;
        if (match.Success && Int64.TryParse(match.Groups[1].Value, out milliseconds))
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
        return DateTime.UtcNow;
    }

    private static List<NoteAiResult> ReadAiResults(Dictionary<string, object> raw)
    {
        var results = new List<NoteAiResult>();
        object value;
        object[] values;
        if (raw == null || !raw.TryGetValue("AiResults", out value) ||
            (values = value as object[]) == null) return results;
        foreach (object item in values)
        {
            Dictionary<string, object> rawResult = item as Dictionary<string, object>;
            if (rawResult == null) continue;
            var result = new NoteAiResult
            {
                Id = ReadString(rawResult, "Id"),
                Operation = ReadString(rawResult, "Operation"),
                Output = ReadString(rawResult, "Output"),
                Model = ReadString(rawResult, "Model"),
                SourceRevision = ReadLong(rawResult, "SourceRevision", 0),
                SourceNoteIds = ReadStringList(rawResult, "SourceNoteIds"),
                SourceRevisions = ReadLongList(rawResult, "SourceRevisions"),
                PromptVersion = ReadString(rawResult, "PromptVersion"),
                ProviderId = ReadString(rawResult, "ProviderId"),
                Status = ReadString(rawResult, "Status"),
                CreatedAtUtc = ReadUtc(rawResult, "CreatedAtUtc")
            };
            if (result.SourceNoteIds == null) result.SourceNoteIds = new List<string>();
            if (result.SourceRevisions == null) result.SourceRevisions = new List<long>();
            if (string.IsNullOrWhiteSpace(result.Status)) result.Status = "saved";
            result.UnknownFields = CopyUnknownFields(rawResult, AiResultFields);
            results.Add(result);
        }
        return results;
    }

    private static bool IsValidAiResultsArray(Dictionary<string, object> raw)
    {
        object value;
        if (!raw.TryGetValue("AiResults", out value) || value == null) return true;
        object[] values = value as object[];
        if (values == null) return false;
        foreach (object item in values)
            if (!(item is Dictionary<string, object>)) return false;
        return true;
    }

    private static List<Dictionary<string, object>> SerializeAiResults(List<NoteAiResult> values)
    {
        var results = new List<Dictionary<string, object>>();
        foreach (NoteAiResult result in values ?? new List<NoteAiResult>())
        {
            if (result == null) continue;
            var raw = NotesValueCopy.CopyDictionary(result.UnknownFields);
            raw["Id"] = result.Id ?? ""; raw["Operation"] = result.Operation ?? "";
            raw["Output"] = result.Output ?? ""; raw["Model"] = result.Model ?? "";
            raw["SourceRevision"] = result.SourceRevision;
            raw["SourceNoteIds"] = result.SourceNoteIds == null ? new List<string>() : new List<string>(result.SourceNoteIds);
            raw["SourceRevisions"] = result.SourceRevisions == null ? new List<long>() : new List<long>(result.SourceRevisions);
            raw["PromptVersion"] = result.PromptVersion ?? "";
            raw["ProviderId"] = result.ProviderId ?? "";
            raw["Status"] = string.IsNullOrWhiteSpace(result.Status) ? "saved" : result.Status;
            raw["CreatedAtUtc"] = result.CreatedAtUtc.ToUniversalTime().ToString("o");
            results.Add(raw);
        }
        return results;
    }

    private static Dictionary<string, object> CopyUnknownFields(Dictionary<string, object> raw,
        HashSet<string> known)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw == null) return copy;
        foreach (KeyValuePair<string, object> pair in raw)
            if (!known.Contains(pair.Key)) copy[pair.Key] = NotesValueCopy.CopyValue(pair.Value);
        return copy;
    }
}
