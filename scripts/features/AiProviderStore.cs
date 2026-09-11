using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class AiProviderStore
{
    internal const int CurrentSchemaVersion = 1;
    private static readonly HashSet<string> RootFields = new HashSet<string>(
        new[] { "schemaVersion", "defaultProviderId", "providers" }, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ProviderFields = new HashSet<string>(
        new[] { "id", "name", "protocol", "baseUrl", "model", "isDefault" }, StringComparer.OrdinalIgnoreCase);
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VibeFlow-AI-Credential-v1");
    private readonly string path;
    private readonly string backupPath;
    private readonly string secretPath;
    private readonly string secretBackupPath;

    internal AiProviderStore(string userStateRoot)
    {
        Directory.CreateDirectory(userStateRoot);
        path = Path.Combine(userStateRoot, "ai-providers.json");
        backupPath = path + ".bak";
        secretPath = Path.Combine(userStateRoot, "ai-providers.secrets");
        secretBackupPath = secretPath + ".bak";
    }

    internal string ConfigPath { get { return path; } }
    internal string SecretPath { get { return secretPath; } }

    internal AiProviderLoadResult Load()
    {
        AiProviderDocument document;
        if (TryRead(path, out document))
        {
            document = Normalize(document);
            document.RecoveredFromBackup = false;
            return AiProviderLoadResult.Success(document, false);
        }
        if (TryRead(backupPath, out document))
        {
            document = Normalize(document);
            document.RecoveredFromBackup = true;
            return AiProviderLoadResult.Success(document, true);
        }
        if (!File.Exists(path) && !File.Exists(backupPath)) return AiProviderLoadResult.Success(new AiProviderDocument(), false);
        return AiProviderLoadResult.Failure("AI-PROVIDER-CONFIG-CORRUPT");
    }

    internal bool TrySave(AiProviderDocument document, out string errorCode)
    {
        errorCode = "";
        try
        {
            AiProviderDocument safe = Normalize(document);
            foreach (AiProviderProfile profile in safe.Providers)
            {
                if (profile == null || !profile.TryValidate(out errorCode)) return false;
            }
            bool preserveValidBackup = safe.RecoveredFromBackup && File.Exists(backupPath);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, Serialize(safe), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, preserveValidBackup ? null : backupPath, true);
            else
            {
                File.Move(temp, path);
                if (!preserveValidBackup || !File.Exists(backupPath)) File.Copy(path, backupPath, true);
            }
            safe.RecoveredFromBackup = false;
            return true;
        }
        catch
        {
            errorCode = "AI-PROVIDER-CONFIG-WRITE-FAILED";
            return false;
        }
    }

    internal bool TrySaveApiKey(string providerId, string apiKey, out string errorCode)
    {
        errorCode = "";
        if (string.IsNullOrWhiteSpace(providerId) || (apiKey ?? "").Length > 4096)
        {
            errorCode = "AI-KEY-INVALID";
            return false;
        }
        try
        {
            bool recoveredFromBackup;
            Dictionary<string, string> secrets = ReadSecrets(out recoveredFromBackup);
            if (string.IsNullOrEmpty(apiKey)) secrets.Remove(providerId);
            else
            {
                byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), Entropy,
                    DataProtectionScope.CurrentUser);
                secrets[providerId] = Convert.ToBase64String(protectedBytes);
            }
            return WriteSecrets(secrets, recoveredFromBackup, out errorCode);
        }
        catch
        {
            errorCode = "AI-KEY-PROTECT-FAILED";
            return false;
        }
    }

    internal string ReadApiKey(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return "";
        try
        {
            Dictionary<string, string> primary;
            string encoded;
            if (TryReadSecretsFile(secretPath, out primary))
            {
                if (!primary.TryGetValue(providerId, out encoded) || string.IsNullOrWhiteSpace(encoded)) return "";
                string value;
                if (TryUnprotect(encoded, out value)) return value;
            }
            Dictionary<string, string> backup;
            if (TryReadSecretsFile(secretBackupPath, out backup) &&
                backup.TryGetValue(providerId, out encoded) && !string.IsNullOrWhiteSpace(encoded))
            {
                string value;
                if (TryUnprotect(encoded, out value)) return value;
            }
            return "";
        }
        catch { return ""; }
    }

    internal bool TryDeleteApiKey(string providerId, out string errorCode)
    {
        return TrySaveApiKey(providerId, "", out errorCode);
    }

    private Dictionary<string, string> ReadSecrets(out bool recoveredFromBackup)
    {
        recoveredFromBackup = false;
        Dictionary<string, string> result;
        if (TryReadSecretsFile(secretPath, out result)) return result;
        if (TryReadSecretsFile(secretBackupPath, out result))
        {
            recoveredFromBackup = true;
            return result;
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryUnprotect(string encoded, out string value)
    {
        value = "";
        try
        {
            byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encoded), Entropy,
                DataProtectionScope.CurrentUser);
            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch { return false; }
    }

    private static bool TryReadSecretsFile(string source, out Dictionary<string, string> result)
    {
        result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(source)) return false;
        try
        {
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(source, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;
            foreach (KeyValuePair<string, object> pair in raw)
                if (pair.Value != null) result[pair.Key] = Convert.ToString(pair.Value);
            return true;
        }
        catch { return false; }
    }

    private bool WriteSecrets(Dictionary<string, string> secrets, bool preserveBackup, out string errorCode)
    {
        errorCode = "";
        try
        {
            string temp = secretPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(secrets), new UTF8Encoding(false));
            if (File.Exists(secretPath))
            {
                // When the primary was recovered from a valid backup, keep that
                // backup intact while atomically replacing the damaged primary.
                File.Replace(temp, secretPath, preserveBackup ? null : secretBackupPath, true);
            }
            else
            {
                File.Move(temp, secretPath);
                if (!preserveBackup || !File.Exists(secretBackupPath))
                    File.Copy(secretPath, secretBackupPath, true);
            }
            return true;
        }
        catch
        {
            errorCode = "AI-KEY-WRITE-FAILED";
            return false;
        }
    }

    private static AiProviderDocument Normalize(AiProviderDocument document)
    {
        AiProviderDocument normalized = document ?? new AiProviderDocument();
        normalized.SchemaVersion = CurrentSchemaVersion;
        if (normalized.UnknownFields == null) normalized.UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (normalized.Providers == null) normalized.Providers = new List<AiProviderProfile>();
        normalized.Providers = normalized.Providers.Where(profile => profile != null).Select(profile =>
        {
            profile.Id = (profile.Id ?? "").Trim();
            profile.Name = (profile.Name ?? "").Trim();
            profile.Protocol = (profile.Protocol ?? "openai-chat-completions").Trim().ToLowerInvariant();
            profile.BaseUrl = (profile.BaseUrl ?? "").Trim().TrimEnd('/');
            profile.Model = (profile.Model ?? "").Trim();
            if (profile.UnknownFields == null) profile.UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return profile;
        }).ToList();
        if (string.IsNullOrWhiteSpace(normalized.DefaultProviderId))
        {
            AiProviderProfile defaultProfile = normalized.Providers.FirstOrDefault(profile => profile.IsDefault);
            normalized.DefaultProviderId = defaultProfile == null ? "" : defaultProfile.Id;
        }
        return normalized;
    }

    private static bool TryRead(string source, out AiProviderDocument document)
    {
        document = null;
        try
        {
            if (!File.Exists(source)) return false;
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(
                File.ReadAllText(source, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;
            int schema;
            if (!TryReadSchemaVersion(raw, out schema) || schema < 0) return false;
            if (schema > CurrentSchemaVersion) return false;
            document = new AiProviderDocument
            {
                SchemaVersion = schema,
                DefaultProviderId = ReadString(raw, "defaultProviderId"),
                UnknownFields = CopyUnknownFields(raw, RootFields)
            };
            object rawProviders;
            if (!raw.TryGetValue("providers", out rawProviders)) return false;
            object[] values = rawProviders as object[];
            if (values == null) return false;
            foreach (object value in values)
            {
                Dictionary<string, object> item = value as Dictionary<string, object>;
                if (item == null) return false;
                document.Providers.Add(new AiProviderProfile
                {
                    Id = ReadString(item, "id"), Name = ReadString(item, "name"),
                    Protocol = ReadString(item, "protocol"), BaseUrl = ReadString(item, "baseUrl"),
                    Model = ReadString(item, "model"), IsDefault = ReadBool(item, "isDefault", false),
                    UnknownFields = CopyUnknownFields(item, ProviderFields)
                });
            }
            return true;
        }
        catch { return false; }
    }

    private static string Serialize(AiProviderDocument document)
    {
        var raw = NotesValueCopy.CopyDictionary(document.UnknownFields);
        raw["schemaVersion"] = document.SchemaVersion;
        raw["defaultProviderId"] = document.DefaultProviderId ?? "";
        var providers = new List<Dictionary<string, object>>();
        foreach (AiProviderProfile profile in document.Providers)
        {
            var provider = NotesValueCopy.CopyDictionary(profile.UnknownFields);
            provider["id"] = profile.Id;
            provider["name"] = profile.Name;
            provider["protocol"] = profile.Protocol;
            provider["baseUrl"] = profile.BaseUrl;
            provider["model"] = profile.Model;
            provider["isDefault"] = profile.IsDefault;
            providers.Add(provider);
        }
        raw["providers"] = providers;
        return new JavaScriptSerializer().Serialize(raw);
    }

    private static string ReadString(Dictionary<string, object> raw, string key)
    {
        object value;
        return raw != null && raw.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
    }

    private static Dictionary<string, object> CopyUnknownFields(Dictionary<string, object> raw, HashSet<string> known)
    {
        var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw == null) return copy;
        foreach (KeyValuePair<string, object> pair in raw)
            if (known == null || !known.Contains(pair.Key)) copy[pair.Key] = NotesValueCopy.CopyValue(pair.Value);
        return copy;
    }

    private static int ReadInt(Dictionary<string, object> raw, string key, int fallback)
    {
        int value;
        return Int32.TryParse(ReadString(raw, key), out value) ? value : fallback;
    }

    private static bool TryReadSchemaVersion(Dictionary<string, object> raw, out int schema)
    {
        schema = 0;
        object value;
        return raw != null && raw.TryGetValue("schemaVersion", out value) && value != null &&
            Int32.TryParse(Convert.ToString(value), out schema);
    }

    private static bool ReadBool(Dictionary<string, object> raw, string key, bool fallback)
    {
        bool value;
        return Boolean.TryParse(ReadString(raw, key), out value) ? value : fallback;
    }
}
