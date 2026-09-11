using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class FocusTargetStore
{
    internal const int CurrentSchemaVersion = 1;

    private static readonly HashSet<string> RootFields = new HashSet<string>(
        new[] { "schemaVersion", "defaultTargetId", "targets" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TargetFields = new HashSet<string>(
        new[] { "id", "name", "processName", "automationId", "controlType", "className",
            "parentFingerprint", "strategy", "lastVerifiedUtc" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SensitiveFields = new HashSet<string>(
        new[] { "value", "text", "windowTitle", "title", "helpText", "runtimeId", "screenX",
            "screenY", "x", "y", "coordinates", "boundingRectangle" }, StringComparer.OrdinalIgnoreCase);
    private static readonly object StoreLocksGuard = new object();
    private static readonly Dictionary<string, object> StoreLocks =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    private readonly string storePath;
    private readonly string backupPath;
    private readonly object storeLock;

    internal FocusTargetStore(string userStateRoot)
    {
        if (string.IsNullOrWhiteSpace(userStateRoot))
            throw new ArgumentException("A user data directory is required", "userStateRoot");
        string root = Path.GetFullPath(userStateRoot);
        storePath = Path.Combine(root, "focus-targets.json");
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

    internal FocusTargetLoadResult Load()
    {
        lock (storeLock) return LoadCore();
    }

    private FocusTargetLoadResult LoadCore()
    {
        FocusTargetDocument document;
        bool migrated;
        if (File.Exists(storePath) && TryReadDocument(storePath, out document, out migrated))
        {
            document.StorageRevision = ReadStorageRevision(storePath);
            if (document.StorageRevision == null)
                return FocusTargetLoadResult.Failure("FOCUS-STORE-READ-FAILED");
            document.StorageRecoveredFromBackup = false;
            return FocusTargetLoadResult.Success(document, migrated, false);
        }
        if (File.Exists(backupPath) && TryReadDocument(backupPath, out document, out migrated))
        {
            document.StorageRevision = ReadStorageRevision(storePath);
            if (document.StorageRevision == null)
                return FocusTargetLoadResult.Failure("FOCUS-STORE-READ-FAILED");
            document.StorageRecoveredFromBackup = true;
            return FocusTargetLoadResult.Success(document, migrated, true);
        }
        if (!File.Exists(storePath) && !File.Exists(backupPath))
        {
            document = new FocusTargetDocument();
            document.StorageRevision = "";
            document.StorageRecoveredFromBackup = false;
            return FocusTargetLoadResult.Success(document, false, false);
        }
        return FocusTargetLoadResult.Failure("FOCUS-STORE-CORRUPT");
    }

    internal bool TrySave(FocusTargetDocument document, out string errorCode)
    {
        lock (storeLock) return TrySaveCore(document, out errorCode);
    }

    private bool TrySaveCore(FocusTargetDocument document, out string errorCode)
    {
        errorCode = "";
        if (document == null)
        {
            errorCode = "FOCUS-STORE-DOCUMENT-MISSING";
            return false;
        }
        if (document.IsFutureSchema || document.SchemaVersion > CurrentSchemaVersion)
        {
            errorCode = "FOCUS-SCHEMA-NEWER";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FocusTargetDescriptor target in document.Targets)
        {
            if (target == null || !target.TryValidateForStorage(out errorCode)) return false;
            target.NormalizeForStorage();
            if (!ids.Add(target.Id))
            {
                errorCode = "FOCUS-TARGET-ID-DUPLICATE";
                return false;
            }
        }
        if (!string.IsNullOrWhiteSpace(document.DefaultTargetId) && !ids.Contains(document.DefaultTargetId))
        {
            errorCode = "FOCUS-DEFAULT-TARGET-MISSING";
            return false;
        }

        try
        {
            string directory = Path.GetDirectoryName(storePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorCode = "FOCUS-STORE-PATH-INVALID";
                return false;
            }
            Directory.CreateDirectory(directory);
            string currentRevision = ReadStorageRevision(storePath);
            if (currentRevision == null)
            {
                errorCode = "FOCUS-STORE-READ-FAILED";
                return false;
            }
            string expectedRevision = document.StorageRevision;
            if (expectedRevision == null && !File.Exists(storePath) && !File.Exists(backupPath))
                expectedRevision = "";
            if (!string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
            {
                errorCode = "FOCUS-STORE-CONFLICT";
                return false;
            }
            document.SchemaVersion = CurrentSchemaVersion;
            Dictionary<string, object> serialized = SerializeDocument(document);
            string json = new JavaScriptSerializer().Serialize(serialized);
            WriteTextAtomically(storePath, json, backupPath, document.StorageRecoveredFromBackup);
            document.StorageRevision = ComputeStorageRevision(json);
            document.StorageRecoveredFromBackup = false;
            return true;
        }
        catch
        {
            errorCode = "FOCUS-STORE-WRITE-FAILED";
            return false;
        }
    }

    private static bool TryReadDocument(string path, out FocusTargetDocument document, out bool migrated)
    {
        document = null;
        migrated = false;
        try
        {
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;

            int sourceSchema;
            if (!TryReadSchemaVersion(raw, out sourceSchema)) return false;
            document = new FocusTargetDocument();
            document.SchemaVersion = sourceSchema > CurrentSchemaVersion ? sourceSchema : CurrentSchemaVersion;
            document.IsFutureSchema = sourceSchema > CurrentSchemaVersion;
            document.DefaultTargetId = ReadString(raw, "defaultTargetId");
            document.UnknownFields = CopyUnknownFields(raw, RootFields);
            migrated = sourceSchema < CurrentSchemaVersion;

            object targetValue;
            if (!raw.TryGetValue("targets", out targetValue)) return false;
            object[] targets = targetValue as object[];
            if (targets == null && targetValue is System.Collections.ArrayList)
                targets = ((System.Collections.ArrayList)targetValue).ToArray();
            if (targets == null) return false;
            var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object rawItem in targets)
            {
                Dictionary<string, object> rawTarget = rawItem as Dictionary<string, object>;
                if (rawTarget == null) return false;
                FocusTargetDescriptor target = DeserializeTarget(rawTarget);
                string validationError;
                if (!document.IsFutureSchema)
                {
                    if (!target.TryValidateForStorage(out validationError)) return false;
                    target.NormalizeForStorage();
                    if (!targetIds.Add(target.Id)) return false;
                }
                document.Targets.Add(target);
                if (!string.Equals(ReadString(rawTarget, "processName"), target.ProcessName,
                    StringComparison.Ordinal)) migrated = true;
            }
            if (!document.IsFutureSchema && !string.IsNullOrWhiteSpace(document.DefaultTargetId) &&
                document.DefaultTarget() == null)
            {
                document.DefaultTargetId = "";
                migrated = true;
            }
            return true;
        }
        catch
        {
            document = null;
            migrated = false;
            return false;
        }
    }

    private static FocusTargetDescriptor DeserializeTarget(Dictionary<string, object> raw)
    {
        var target = new FocusTargetDescriptor();
        target.Id = ReadString(raw, "id");
        target.Name = ReadString(raw, "name");
        target.ProcessName = ReadString(raw, "processName");
        target.AutomationId = ReadString(raw, "automationId");
        target.ControlType = ReadString(raw, "controlType");
        target.ClassName = ReadString(raw, "className");
        target.ParentFingerprint = ReadString(raw, "parentFingerprint");
        target.Strategy = ReadString(raw, "strategy");
        if (string.IsNullOrWhiteSpace(target.Strategy)) target.Strategy = "uia";
        string verified = ReadString(raw, "lastVerifiedUtc");
        DateTime parsed;
        if (DateTime.TryParse(verified, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
            target.LastVerifiedUtc = parsed.ToUniversalTime();
        target.UnknownFields = CopyUnknownFields(raw, TargetFields);
        return target;
    }

    private static Dictionary<string, object> SerializeDocument(FocusTargetDocument document)
    {
        Dictionary<string, object> root = CopySafeDictionary(document.UnknownFields);
        root["schemaVersion"] = CurrentSchemaVersion;
        root["defaultTargetId"] = (document.DefaultTargetId ?? "").Trim();
        var targets = new List<object>();
        foreach (FocusTargetDescriptor target in document.Targets)
        {
            Dictionary<string, object> item = CopySafeDictionary(target.UnknownFields);
            item["id"] = target.Id;
            item["name"] = target.Name;
            item["processName"] = target.ProcessName;
            item["automationId"] = target.AutomationId;
            item["controlType"] = target.ControlType;
            item["className"] = target.ClassName;
            item["parentFingerprint"] = target.ParentFingerprint;
            item["strategy"] = target.Strategy;
            item["lastVerifiedUtc"] = target.LastVerifiedUtc.HasValue
                ? target.LastVerifiedUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : "";
            targets.Add(item);
        }
        root["targets"] = targets.ToArray();
        return root;
    }

    private static Dictionary<string, object> CopyUnknownFields(Dictionary<string, object> source,
        HashSet<string> knownFields)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (source == null) return copy;
        foreach (KeyValuePair<string, object> pair in source)
        {
            if (knownFields.Contains(pair.Key) || SensitiveFields.Contains(pair.Key)) continue;
            object safeValue;
            if (TryCopySafeValue(pair.Value, out safeValue)) copy[pair.Key] = safeValue;
        }
        return copy;
    }

    private static Dictionary<string, object> CopySafeDictionary(Dictionary<string, object> source)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (source == null) return copy;
        foreach (KeyValuePair<string, object> pair in source)
        {
            if (SensitiveFields.Contains(pair.Key)) continue;
            object safeValue;
            if (TryCopySafeValue(pair.Value, out safeValue)) copy[pair.Key] = safeValue;
        }
        return copy;
    }

    private static bool TryCopySafeValue(object value, out object safeValue)
    {
        Dictionary<string, object> dictionary = value as Dictionary<string, object>;
        if (dictionary != null)
        {
            safeValue = CopySafeDictionary(dictionary);
            return true;
        }
        object[] array = value as object[];
        if (array != null)
        {
            var safeItems = new List<object>();
            foreach (object item in array)
            {
                object safeItem;
                if (TryCopySafeValue(item, out safeItem)) safeItems.Add(safeItem);
            }
            safeValue = safeItems.ToArray();
            return true;
        }
        if (value == null || value is string || value is bool || value is int || value is long ||
            value is double || value is decimal)
        {
            safeValue = value;
            return true;
        }
        safeValue = null;
        return false;
    }

    private static string ReadString(Dictionary<string, object> source, string key)
    {
        object value;
        return source != null && source.TryGetValue(key, out value) ? Convert.ToString(value) ?? "" : "";
    }

    private static bool TryReadSchemaVersion(Dictionary<string, object> source, out int schemaVersion)
    {
        schemaVersion = 0;
        object value;
        return source != null && source.TryGetValue("schemaVersion", out value) && value != null &&
            int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out schemaVersion) && schemaVersion >= 0;
    }

    private static void WriteTextAtomically(string path, string content, string backup,
        bool preserveExistingBackup)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, preserveExistingBackup ? null : backup);
            else File.Move(temp, path);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
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
