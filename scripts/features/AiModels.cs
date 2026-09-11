using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

internal enum AiOperationKind
{
    Organize = 0,
    Classify = 1,
    Summarize = 2,
    Translate = 3
}

internal sealed class AiProviderProfile
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Protocol { get; set; }
    public string BaseUrl { get; set; }
    public string Model { get; set; }
    public bool IsDefault { get; set; }
    public Dictionary<string, object> UnknownFields { get; set; }

    internal AiProviderProfile()
    {
        Id = Guid.NewGuid().ToString("N");
        Name = "我的文本模型";
        Protocol = "openai-chat-completions";
        BaseUrl = "https://api.openai.com/v1";
        Model = "";
        IsDefault = false;
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal AiProviderProfile Copy()
    {
        return new AiProviderProfile
        {
            Id = Id,
            Name = Name,
            Protocol = Protocol,
            BaseUrl = BaseUrl,
            Model = Model,
            IsDefault = IsDefault,
            UnknownFields = NotesValueCopy.CopyDictionary(UnknownFields)
        };
    }

    internal bool TryValidate(out string errorCode)
    {
        errorCode = "";
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64)
        {
            errorCode = "AI-PROVIDER-ID-INVALID";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 80)
        {
            errorCode = "AI-PROVIDER-NAME-INVALID";
            return false;
        }
        if (!string.Equals(Protocol, "openai-chat-completions", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "AI-PROTOCOL-UNSUPPORTED";
            return false;
        }
        Uri endpoint;
        return AiEndpointPolicy.TryBuildChatCompletionsEndpoint(BaseUrl, out endpoint, out errorCode) &&
            !string.IsNullOrWhiteSpace(Model) && Model.Trim().Length <= 160;
    }
}

internal sealed class AiProviderDocument
{
    public int SchemaVersion { get; set; }
    public string DefaultProviderId { get; set; }
    public List<AiProviderProfile> Providers { get; internal set; }
    public Dictionary<string, object> UnknownFields { get; set; }
    // Runtime-only marker used to avoid replacing a valid .bak after loading
    // a damaged primary provider document.
    internal bool RecoveredFromBackup { get; set; }

    internal AiProviderDocument()
    {
        SchemaVersion = AiProviderStore.CurrentSchemaVersion;
        DefaultProviderId = "";
        Providers = new List<AiProviderProfile>();
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed class AiPromptLoadResult
{
    internal bool IsSuccess { get; private set; }
    internal bool RecoveredFromBackup { get; private set; }
    internal string ErrorCode { get; private set; }
    internal string CustomSystemPrompt { get; private set; }
    internal Dictionary<string, object> UnknownFields { get; private set; }

    internal static AiPromptLoadResult Success(string prompt, bool recovered,
        Dictionary<string, object> unknownFields)
    {
        return new AiPromptLoadResult
        {
            IsSuccess = true,
            RecoveredFromBackup = recovered,
            ErrorCode = "",
            CustomSystemPrompt = prompt ?? "",
            UnknownFields = unknownFields ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };
    }

    internal static AiPromptLoadResult Failure(string errorCode)
    {
        return new AiPromptLoadResult
        {
            IsSuccess = false,
            RecoveredFromBackup = false,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "AI-PROMPT-READ-FAILED" : errorCode,
            CustomSystemPrompt = "",
            UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// Stores the optional, user-authored local text-organization prompt separately
/// from provider credentials and the frozen voice configuration. The prompt is
/// ordinary user preference data; it never contains an API key or note content.
/// </summary>
internal sealed class AiPromptStore
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaximumPromptLength = 4000;
    private static readonly HashSet<string> KnownFields = new HashSet<string>(
        new[] { "schemaVersion", "customSystemPrompt" }, StringComparer.OrdinalIgnoreCase);
    private readonly object sync = new object();
    private readonly string path;
    private readonly string backupPath;
    private readonly string root;

    internal AiPromptStore(string userStateRoot)
    {
        root = Path.GetFullPath(userStateRoot ?? AppDomain.CurrentDomain.BaseDirectory);
        Directory.CreateDirectory(root);
        path = Path.Combine(root, "ai-prompts.json");
        backupPath = path + ".bak";
    }

    internal string FilePath { get { return path; } }

    internal AiPromptLoadResult Load()
    {
        lock (sync)
        {
            AiPromptLoadResult result;
            if (TryRead(path, out result)) return result;
            AiPromptLoadResult backup;
            if (TryRead(backupPath, out backup))
                return AiPromptLoadResult.Success(backup.CustomSystemPrompt, true, backup.UnknownFields);
            if (!File.Exists(path) && !File.Exists(backupPath))
                return AiPromptLoadResult.Success("", false, null);
            return AiPromptLoadResult.Failure("AI-PROMPT-READ-FAILED");
        }
    }

    internal bool TrySave(string customSystemPrompt, out string errorCode)
    {
        lock (sync)
        {
            errorCode = "";
            string normalized;
            if (!TryNormalize(customSystemPrompt, out normalized, out errorCode)) return false;
            AiPromptLoadResult current = Load();
            if (current == null || !current.IsSuccess)
            {
                errorCode = current == null || string.IsNullOrWhiteSpace(current.ErrorCode)
                    ? "AI-PROMPT-READ-FAILED" : current.ErrorCode;
                return false;
            }
            bool preserveValidBackup = current.RecoveredFromBackup && File.Exists(backupPath);
            Dictionary<string, object> raw = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (current.UnknownFields != null)
                foreach (KeyValuePair<string, object> pair in current.UnknownFields)
                    raw[pair.Key] = NotesValueCopy.CopyValue(pair.Value);
            raw["schemaVersion"] = CurrentSchemaVersion;
            raw["customSystemPrompt"] = normalized;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(raw);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try { File.Replace(temporary, path, preserveValidBackup ? null : backupPath, true); }
                    catch
                    {
                        if (preserveValidBackup)
                        {
                            File.Delete(path);
                        }
                        else
                        {
                            if (File.Exists(backupPath)) File.Delete(backupPath);
                            File.Move(path, backupPath);
                        }
                        File.Move(temporary, path);
                    }
                }
                else File.Move(temporary, path);
                return true;
            }
            catch
            {
                errorCode = "AI-PROMPT-WRITE-FAILED";
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                return false;
            }
        }
    }

    internal bool TryReset(out string errorCode)
    {
        return TrySave("", out errorCode);
    }

    private static bool TryNormalize(string value, out string normalized, out string errorCode)
    {
        normalized = (value ?? "").Replace("\0", "").Trim();
        if (normalized.Length > MaximumPromptLength)
        {
            errorCode = "AI-PROMPT-TOO-LONG";
            return false;
        }
        foreach (char character in normalized)
        {
            if (Char.IsControl(character) && character != '\r' && character != '\n' && character != '\t')
            {
                errorCode = "AI-PROMPT-CONTROL-CHARACTER";
                return false;
            }
        }
        errorCode = "";
        return true;
    }

    private static bool TryRead(string candidate, out AiPromptLoadResult result)
    {
        result = null;
        try
        {
            if (!File.Exists(candidate)) return false;
            Dictionary<string, object> raw = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }
                .DeserializeObject(File.ReadAllText(candidate, Encoding.UTF8)) as Dictionary<string, object>;
            if (raw == null) return false;
            int schema;
            if (!TryReadSchemaVersion(raw, out schema) || schema < 0) return false;
            if (schema > CurrentSchemaVersion) return false;
            object rawPrompt;
            if (!raw.TryGetValue("customSystemPrompt", out rawPrompt) || !(rawPrompt is string)) return false;
            string prompt = (string)rawPrompt;
            string normalized;
            string ignored;
            if (!TryNormalize(prompt, out normalized, out ignored)) return false;
            var unknown = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in raw)
                if (!KnownFields.Contains(pair.Key)) unknown[pair.Key] = NotesValueCopy.CopyValue(pair.Value);
            result = AiPromptLoadResult.Success(normalized, false, unknown);
            return true;
        }
        catch { return false; }
    }

    private static int ReadInt(Dictionary<string, object> raw, string key, int fallback)
    {
        object value;
        int parsed;
        return raw != null && raw.TryGetValue(key, out value) && Int32.TryParse(Convert.ToString(value), out parsed)
            ? parsed : fallback;
    }

    private static bool TryReadSchemaVersion(Dictionary<string, object> raw, out int schema)
    {
        schema = 0;
        object value;
        return raw != null && raw.TryGetValue("schemaVersion", out value) && value != null &&
            Int32.TryParse(Convert.ToString(value), out schema);
    }
}

internal sealed class AiProviderLoadResult
{
    internal bool IsSuccess { get; private set; }
    internal bool RecoveredFromBackup { get; private set; }
    internal string ErrorCode { get; private set; }
    internal AiProviderDocument Document { get; private set; }

    internal static AiProviderLoadResult Success(AiProviderDocument document, bool recovered)
    {
        return new AiProviderLoadResult
        {
            IsSuccess = true,
            RecoveredFromBackup = recovered,
            ErrorCode = "",
            Document = document ?? new AiProviderDocument()
        };
    }

    internal static AiProviderLoadResult Failure(string errorCode)
    {
        return new AiProviderLoadResult
        {
            IsSuccess = false,
            RecoveredFromBackup = false,
            ErrorCode = errorCode ?? "AI-PROVIDER-READ-FAILED",
            Document = new AiProviderDocument()
        };
    }
}

internal sealed class AiTextRequest
{
    public AiOperationKind Operation { get; set; }
    public string SourceText { get; set; }
    public string TargetLanguage { get; set; }
    public string SourceTitle { get; set; }
    public string NoteId { get; set; }
    public List<string> ExistingCategories { get; set; }
    public List<string> SourceNoteIds { get; set; }
    public List<long> SourceRevisions { get; set; }
}

internal sealed class AiTextResult
{
    public bool IsSuccess { get; private set; }
    public bool IsCanceled { get; private set; }
    public string Output { get; private set; }
    public string Message { get; private set; }
    public string ErrorCode { get; private set; }
    public string Model { get; private set; }
    public string RetryAfter { get; private set; }

    internal static AiTextResult Success(string output, string model)
    {
        return new AiTextResult { IsSuccess = true, Output = output ?? "", Message = "模型返回了可预览结果", ErrorCode = "", Model = model ?? "", RetryAfter = "" };
    }

    internal static AiTextResult Failure(string message, string code)
    {
        return Failure(message, code, "");
    }

    internal static AiTextResult Failure(string message, string code, string retryAfter)
    {
        return new AiTextResult { IsSuccess = false, Output = "", Message = message ?? "模型请求失败", ErrorCode = code ?? "AI-REQUEST-FAILED", Model = "", RetryAfter = retryAfter ?? "" };
    }

    internal static AiTextResult Canceled()
    {
        return new AiTextResult { IsSuccess = false, IsCanceled = true, Output = "", Message = "模型请求已取消", ErrorCode = "AI-CANCELED", Model = "", RetryAfter = "" };
    }
}

internal static class AiEndpointPolicy
{
    internal static bool TryBuildChatCompletionsEndpoint(string baseUrl, out Uri endpoint, out string errorCode)
    {
        endpoint = null;
        errorCode = "";
        Uri parsed;
        if (!Uri.TryCreate((baseUrl ?? "").Trim(), UriKind.Absolute, out parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrWhiteSpace(parsed.UserInfo) || !string.IsNullOrWhiteSpace(parsed.Fragment) ||
            !string.IsNullOrWhiteSpace(parsed.Query))
        {
            errorCode = "AI-ENDPOINT-INVALID";
            return false;
        }
        bool loopback = string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parsed.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parsed.Host, "[::1]", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parsed.Host, "::1", StringComparison.OrdinalIgnoreCase);
        if (parsed.Scheme == Uri.UriSchemeHttp && !loopback)
        {
            errorCode = "AI-ENDPOINT-HTTPS-REQUIRED";
            return false;
        }
        string path = (parsed.AbsolutePath ?? "").TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = parsed;
            return true;
        }
        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) path += "/v1";
        UriBuilder builder = new UriBuilder(parsed);
        builder.Path = path + "/chat/completions";
        endpoint = builder.Uri;
        return true;
    }
}
