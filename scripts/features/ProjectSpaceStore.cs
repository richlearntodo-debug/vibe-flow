using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class ProjectSpaceStore
{
    internal const int CurrentSchemaVersion = 1;

    private static readonly HashSet<string> RootFields = new HashSet<string>(
        new[] { "schemaVersion", "spaces" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SpaceFields = new HashSet<string>(
        new[] { "id", "name", "icon", "editorKind", "editorExecutablePath", "workspacePath",
            "terminalExecutablePath", "previewUrl", "repositoryUrl", "documentationUrl", "profileId",
            "focusTargetId", "captureTargetId", "shortcut", "entryKind", "enabled", "lastUsedUtc" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly object StoreLocksGuard = new object();
    private static readonly Dictionary<string, object> StoreLocks =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    private readonly string storePath;
    private readonly string backupPath;
    private readonly object storeLock;

    internal ProjectSpaceStore(string userStateRoot)
    {
        if (string.IsNullOrWhiteSpace(userStateRoot))
            throw new ArgumentException("A user data directory is required", "userStateRoot");
        string root = Path.GetFullPath(userStateRoot);
        storePath = Path.Combine(root, "project-spaces.json");
        backupPath = storePath + ".bak";
        lock (StoreLocksGuard)
        {
            if (!StoreLocks.TryGetValue(storePath, out storeLock))
            {
                storeLock = new object();
                StoreLocks[storePath] = storeLock;
            }
        }
    }

    internal ProjectSpaceLoadResult Load()
    {
        lock (storeLock) return LoadCore();
    }

    private ProjectSpaceLoadResult LoadCore()
    {
        ProjectSpaceDocument document;
        bool migrated;
        string primaryError = "";
        if (File.Exists(storePath) && TryReadDocument(storePath, out document, out migrated, out primaryError))
        {
            document.StorageRevision = ReadStorageRevision(storePath);
            if (document.StorageRevision == null)
                return ProjectSpaceLoadResult.Failure("PROJECT-STORE-READ-FAILED");
            document.StorageRecoveredFromBackup = false;
            return ProjectSpaceLoadResult.Success(document, migrated, false);
        }
        string backupError = "";
        if (File.Exists(backupPath) && TryReadDocument(backupPath, out document, out migrated, out backupError))
        {
            document.StorageRevision = ReadStorageRevision(storePath);
            if (document.StorageRevision == null)
                return ProjectSpaceLoadResult.Failure("PROJECT-STORE-READ-FAILED");
            document.StorageRecoveredFromBackup = true;
            return ProjectSpaceLoadResult.Success(document, migrated, true);
        }
        if (!File.Exists(storePath) && !File.Exists(backupPath))
        {
            document = new ProjectSpaceDocument();
            document.StorageRevision = "";
            document.StorageRecoveredFromBackup = false;
            return ProjectSpaceLoadResult.Success(document, false, false);
        }
        string failureCode = !string.IsNullOrWhiteSpace(primaryError) &&
            !string.Equals(primaryError, "PROJECT-STORE-CORRUPT", StringComparison.Ordinal)
            ? primaryError : backupError;
        return ProjectSpaceLoadResult.Failure(string.IsNullOrWhiteSpace(failureCode)
            ? "PROJECT-STORE-CORRUPT" : failureCode);
    }

    internal bool TrySave(ProjectSpaceDocument document, out string errorCode)
    {
        lock (storeLock) return TrySaveCore(document, out errorCode);
    }

    private bool TrySaveCore(ProjectSpaceDocument document, out string errorCode)
    {
        errorCode = "";
        if (document == null)
        {
            errorCode = "PROJECT-STORE-DOCUMENT-MISSING";
            return false;
        }
        if (document.IsFutureSchema || document.SchemaVersion > CurrentSchemaVersion)
        {
            errorCode = "PROJECT-SCHEMA-NEWER";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectSpace space in document.Spaces)
        {
            if (space == null)
            {
                errorCode = "PROJECT-SPACE-MISSING";
                return false;
            }
            if (!space.TryValidateForStorage(out errorCode)) return false;
            space.NormalizeForStorage();
            if (!ids.Add(space.Id))
            {
                errorCode = "PROJECT-ID-DUPLICATE";
                return false;
            }
        }
        try
        {
            string directory = Path.GetDirectoryName(storePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorCode = "PROJECT-STORE-PATH-INVALID";
                return false;
            }
            Directory.CreateDirectory(directory);
            string currentRevision = ReadStorageRevision(storePath);
            if (currentRevision == null)
            {
                errorCode = "PROJECT-STORE-READ-FAILED";
                return false;
            }
            string expectedRevision = document.StorageRevision;
            if (expectedRevision == null && !File.Exists(storePath) && !File.Exists(backupPath))
                expectedRevision = "";
            if (!string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
            {
                errorCode = "PROJECT-STORE-CONFLICT";
                return false;
            }
            document.SchemaVersion = CurrentSchemaVersion;
            string json = new JavaScriptSerializer().Serialize(SerializeDocument(document));
            WriteTextAtomically(storePath, json, backupPath, document.StorageRecoveredFromBackup);
            document.StorageRevision = ComputeStorageRevision(json);
            document.StorageRecoveredFromBackup = false;
            return true;
        }
        catch
        {
            errorCode = "PROJECT-STORE-WRITE-FAILED";
            return false;
        }
    }

    private static bool TryReadDocument(string path, out ProjectSpaceDocument document,
        out bool migrated, out string errorCode)
    {
        document = null;
        migrated = false;
        errorCode = "PROJECT-STORE-CORRUPT";
        try
        {
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;
            int sourceSchema;
            if (!TryReadSchemaVersion(raw, out sourceSchema)) return false;
            document = new ProjectSpaceDocument();
            document.SchemaVersion = sourceSchema > CurrentSchemaVersion ? sourceSchema : CurrentSchemaVersion;
            document.IsFutureSchema = sourceSchema > CurrentSchemaVersion;
            document.UnknownFields = CopyUnknownFields(raw, RootFields);
            migrated = sourceSchema < CurrentSchemaVersion;

            object rawSpaces;
            if (raw.TryGetValue("spaces", out rawSpaces))
            {
                object[] spaces = ToObjectArray(rawSpaces);
                if (spaces == null) return false;
                var spaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object rawItem in spaces)
                {
                    Dictionary<string, object> rawSpace = rawItem as Dictionary<string, object>;
                    if (rawSpace == null) return false;
                    ProjectSpace space = DeserializeSpace(rawSpace);
                    string validationError;
                    if (!document.IsFutureSchema)
                    {
                        if (!space.TryValidateStoredStructure(out validationError))
                        {
                            errorCode = validationError;
                            return false;
                        }
                        space.NormalizeForStorage();
                        if (!spaceIds.Add(space.Id))
                        {
                            errorCode = "PROJECT-ID-DUPLICATE";
                            return false;
                        }
                    }
                    document.Spaces.Add(space);
                }
            }
            else if (sourceSchema >= 1) return false;
            return true;
        }
        catch
        {
            document = null;
            migrated = false;
            errorCode = "PROJECT-STORE-CORRUPT";
            return false;
        }
    }

    private static ProjectSpace DeserializeSpace(Dictionary<string, object> raw)
    {
        var space = new ProjectSpace
        {
            Id = ReadString(raw, "id"),
            Name = ReadString(raw, "name"),
            Icon = ReadString(raw, "icon"),
            EditorKind = ReadString(raw, "editorKind"),
            EditorExecutablePath = ReadString(raw, "editorExecutablePath"),
            WorkspacePath = ReadString(raw, "workspacePath"),
            TerminalExecutablePath = ReadString(raw, "terminalExecutablePath"),
            PreviewUrl = ReadString(raw, "previewUrl"),
            RepositoryUrl = ReadString(raw, "repositoryUrl"),
            DocumentationUrl = ReadString(raw, "documentationUrl"),
            ProfileId = ReadString(raw, "profileId"),
            FocusTargetId = ReadString(raw, "focusTargetId"),
            CaptureTargetId = ReadString(raw, "captureTargetId"),
            Shortcut = ReadString(raw, "shortcut"),
            EntryKind = ReadString(raw, "entryKind"),
            Enabled = ReadBool(raw, "enabled", true),
            LastUsedUtc = ReadUtc(raw, "lastUsedUtc"),
            UnknownFields = CopyUnknownFields(raw, SpaceFields)
        };
        return space;
    }

    private static Dictionary<string, object> SerializeDocument(ProjectSpaceDocument document)
    {
        Dictionary<string, object> raw = CopyUnknownFields(document.UnknownFields, RootFields);
        raw["schemaVersion"] = CurrentSchemaVersion;
        var spaces = new List<Dictionary<string, object>>();
        foreach (ProjectSpace space in document.Spaces) spaces.Add(SerializeSpace(space));
        raw["spaces"] = spaces.ToArray();
        return raw;
    }

    private static Dictionary<string, object> SerializeSpace(ProjectSpace space)
    {
        Dictionary<string, object> raw = CopyUnknownFields(space.UnknownFields, SpaceFields);
        raw["id"] = space.Id;
        raw["name"] = space.Name;
        raw["icon"] = space.Icon;
        raw["editorKind"] = space.EditorKind;
        raw["editorExecutablePath"] = space.EditorExecutablePath;
        raw["workspacePath"] = space.WorkspacePath;
        raw["terminalExecutablePath"] = space.TerminalExecutablePath;
        raw["previewUrl"] = space.PreviewUrl;
        raw["repositoryUrl"] = space.RepositoryUrl;
        raw["documentationUrl"] = space.DocumentationUrl;
        raw["profileId"] = space.ProfileId;
        raw["focusTargetId"] = space.FocusTargetId;
        raw["captureTargetId"] = space.CaptureTargetId;
        raw["shortcut"] = space.Shortcut ?? "";
        raw["entryKind"] = string.IsNullOrWhiteSpace(space.EntryKind) ? "project" : space.EntryKind;
        raw["enabled"] = space.Enabled;
        if (space.LastUsedUtc.HasValue)
            raw["lastUsedUtc"] = space.LastUsedUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        return raw;
    }

    private static Dictionary<string, object> CopyUnknownFields(Dictionary<string, object> raw,
        HashSet<string> known)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw == null) return copy;
        foreach (KeyValuePair<string, object> pair in raw)
            if (!known.Contains(pair.Key)) copy[pair.Key] = ProjectSpaceValueCopy.CopyValue(pair.Value);
        return copy;
    }

    private static object[] ToObjectArray(object value)
    {
        object[] array = value as object[];
        if (array != null) return array;
        System.Collections.ArrayList list = value as System.Collections.ArrayList;
        return list == null ? null : list.ToArray();
    }

    private static string ReadString(Dictionary<string, object> raw, string key)
    {
        object value;
        return raw != null && raw.TryGetValue(key, out value) && value != null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
    }

    private static bool TryReadSchemaVersion(Dictionary<string, object> raw, out int schemaVersion)
    {
        schemaVersion = 0;
        object value;
        return raw != null && raw.TryGetValue("schemaVersion", out value) && value != null &&
            int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out schemaVersion) && schemaVersion >= 0;
    }

    private static bool ReadBool(Dictionary<string, object> raw, string key, bool fallback)
    {
        object value;
        bool parsed;
        return raw != null && raw.TryGetValue(key, out value) &&
            bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed)
                ? parsed : fallback;
    }

    private static DateTime? ReadUtc(Dictionary<string, object> raw, string key)
    {
        DateTime parsed;
        return DateTime.TryParse(ReadString(raw, key), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
    }

    private static void WriteTextAtomically(string path, string content, string backupPath,
        bool preserveExistingBackup)
    {
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, content, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporaryPath, path,
                preserveExistingBackup ? null : backupPath);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    private static string ReadStorageRevision(string path)
    {
        if (!File.Exists(path)) return "";
        try { return ComputeStorageRevision(File.ReadAllText(path, Encoding.UTF8)); }
        catch { return null; }
    }

    private static string ComputeStorageRevision(string content)
    {
        using (SHA256 hash = SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content ?? "")));
    }
}
