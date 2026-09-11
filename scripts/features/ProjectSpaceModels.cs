using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

internal sealed class ProjectProfileSnapshot
{
    internal string Id { get; set; }
    internal string Name { get; set; }
    internal string Preset { get; set; }
    internal Dictionary<string, string> Mappings { get; set; }
    internal bool SmartProfilesEnabled { get; set; }
    internal bool SmartProfileLocked { get; set; }

    internal ProjectProfileSnapshot()
    {
        Id = "";
        Name = "";
        Preset = "general";
        Mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal ProjectProfileSnapshot Copy()
    {
        var copy = new ProjectProfileSnapshot
        {
            Id = Id,
            Name = Name,
            Preset = Preset,
            SmartProfilesEnabled = SmartProfilesEnabled,
            SmartProfileLocked = SmartProfileLocked
        };
        if (Mappings != null)
            foreach (KeyValuePair<string, string> pair in Mappings) copy.Mappings[pair.Key] = pair.Value;
        return copy;
    }
}

internal sealed class ProjectSpace
{
    private static readonly Regex IdPattern = new Regex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string EditorKind { get; set; }
    public string EditorExecutablePath { get; set; }
    public string WorkspacePath { get; set; }
    public string TerminalExecutablePath { get; set; }
    public string PreviewUrl { get; set; }
    public string RepositoryUrl { get; set; }
    public string DocumentationUrl { get; set; }
    public string ProfileId { get; set; }
    public string FocusTargetId { get; set; }
    public string CaptureTargetId { get; set; }
    public string Shortcut { get; set; }
    public string EntryKind { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    internal Dictionary<string, object> UnknownFields { get; set; }
    internal FocusTargetDescriptor RuntimeFocusTarget { get; set; }
    internal ProjectProfileSnapshot RuntimeProfile { get; set; }

    public ProjectSpace()
    {
        Id = "";
        Name = "";
        Icon = "";
        EditorKind = "other";
        EditorExecutablePath = "";
        WorkspacePath = "";
        TerminalExecutablePath = "";
        PreviewUrl = "";
        RepositoryUrl = "";
        DocumentationUrl = "";
        ProfileId = "";
        FocusTargetId = "";
        CaptureTargetId = "";
        Shortcut = "";
        EntryKind = "project";
        Enabled = true;
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal ProjectSpace Copy()
    {
        return new ProjectSpace
        {
            Id = Id,
            Name = Name,
            Icon = Icon,
            EditorKind = EditorKind,
            EditorExecutablePath = EditorExecutablePath,
            WorkspacePath = WorkspacePath,
            TerminalExecutablePath = TerminalExecutablePath,
            PreviewUrl = PreviewUrl,
            RepositoryUrl = RepositoryUrl,
            DocumentationUrl = DocumentationUrl,
            ProfileId = ProfileId,
            FocusTargetId = FocusTargetId,
            CaptureTargetId = CaptureTargetId,
            Shortcut = Shortcut,
            EntryKind = EntryKind,
            Enabled = Enabled,
            LastUsedUtc = LastUsedUtc,
            UnknownFields = ProjectSpaceValueCopy.CopyDictionary(UnknownFields),
            RuntimeFocusTarget = RuntimeFocusTarget == null ? null : RuntimeFocusTarget.Copy(),
            RuntimeProfile = RuntimeProfile == null ? null : RuntimeProfile.Copy()
        };
    }

    internal bool TryValidateForStorage(out string errorCode)
    {
        if (!TryValidateStoredStructure(out errorCode)) return false;
        string kind = (EditorKind ?? "").Trim().ToLowerInvariant();

        string normalized;
        if (!ProjectSpaceValidation.TryNormalizeEditorExecutable(EditorExecutablePath, kind,
            out normalized, out errorCode)) return false;
        bool workspaceOptional = string.Equals(EntryKind, "quick-entry", StringComparison.OrdinalIgnoreCase);
        if (!workspaceOptional || !string.IsNullOrWhiteSpace(WorkspacePath))
        {
            if (!ProjectSpaceValidation.TryNormalizeWorkspacePath(WorkspacePath,
                out normalized, out errorCode)) return false;
        }
        if (!string.IsNullOrWhiteSpace(TerminalExecutablePath) &&
            !ProjectSpaceValidation.TryNormalizeApplicationExecutable(TerminalExecutablePath,
                out normalized, out errorCode)) return false;
        if (!string.IsNullOrWhiteSpace(PreviewUrl) &&
            !ProjectSpaceValidation.TryNormalizePreviewUrl(PreviewUrl,
                out normalized, out errorCode)) return false;
        if (!string.IsNullOrWhiteSpace(RepositoryUrl) &&
            !ProjectSpaceValidation.TryNormalizeHttpsUrl(RepositoryUrl,
                out normalized, out errorCode)) return false;
        if (!string.IsNullOrWhiteSpace(DocumentationUrl) &&
            !ProjectSpaceValidation.TryNormalizeHttpsUrl(DocumentationUrl,
                out normalized, out errorCode)) return false;
        errorCode = "";
        return true;
    }

    internal bool TryValidateStoredStructure(out string errorCode)
    {
        if (!IdPattern.IsMatch((Id ?? "").Trim()))
        {
            errorCode = "PROJECT-ID-INVALID";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 80)
        {
            errorCode = "PROJECT-NAME-INVALID";
            return false;
        }
        if ((Icon ?? "").Length > 8)
        {
            errorCode = "PROJECT-ICON-INVALID";
            return false;
        }
        string kind = (EditorKind ?? "").Trim().ToLowerInvariant();
        if (kind != "cursor" && kind != "vscode" && kind != "other" && kind != "chatgpt")
        {
            errorCode = "PROJECT-EDITOR-KIND-INVALID";
            return false;
        }
        if (!ValidOptionalId(ProfileId) || !ValidOptionalId(FocusTargetId) || !ValidOptionalId(CaptureTargetId))
        {
            errorCode = "PROJECT-REFERENCE-ID-INVALID";
            return false;
        }
        if ((Shortcut ?? "").Trim().Length > 80)
        {
            errorCode = "PROJECT-SHORTCUT-INVALID";
            return false;
        }
        string entryKind = string.IsNullOrWhiteSpace(EntryKind) ? "project" : EntryKind.Trim().ToLowerInvariant();
        if (entryKind != "project" && entryKind != "quick-entry")
        {
            errorCode = "PROJECT-ENTRY-KIND-INVALID";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal void NormalizeForStorage()
    {
        Id = (Id ?? "").Trim();
        Name = NormalizeSingleLine(Name, 80);
        Icon = NormalizeSingleLine(Icon, 8);
        EditorKind = (EditorKind ?? "other").Trim().ToLowerInvariant();
        string normalized;
        string ignored;
        if (ProjectSpaceValidation.TryNormalizeEditorExecutable(EditorExecutablePath, EditorKind,
            out normalized, out ignored)) EditorExecutablePath = normalized;
        if (!string.Equals(EntryKind, "quick-entry", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(WorkspacePath))
        {
            if (ProjectSpaceValidation.TryNormalizeWorkspacePath(WorkspacePath,
                out normalized, out ignored)) WorkspacePath = normalized;
        }
        if (string.IsNullOrWhiteSpace(TerminalExecutablePath)) TerminalExecutablePath = "";
        else if (ProjectSpaceValidation.TryNormalizeApplicationExecutable(TerminalExecutablePath,
            out normalized, out ignored)) TerminalExecutablePath = normalized;
        PreviewUrl = NormalizeOptionalUrl(PreviewUrl, true);
        RepositoryUrl = NormalizeOptionalUrl(RepositoryUrl, false);
        DocumentationUrl = NormalizeOptionalUrl(DocumentationUrl, false);
        ProfileId = NormalizeSingleLine(ProfileId, 64);
        FocusTargetId = NormalizeSingleLine(FocusTargetId, 64);
        CaptureTargetId = NormalizeSingleLine(CaptureTargetId, 64);
        Shortcut = NormalizeSingleLine(Shortcut, 80);
        EntryKind = NormalizeSingleLine(EntryKind, 24).ToLowerInvariant();
        if (EntryKind.Length == 0) EntryKind = "project";
        if (UnknownFields == null)
            UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeOptionalUrl(string value, bool preview)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string normalized;
        string ignored;
        bool valid = preview
            ? ProjectSpaceValidation.TryNormalizePreviewUrl(value, out normalized, out ignored)
            : ProjectSpaceValidation.TryNormalizeHttpsUrl(value, out normalized, out ignored);
        return valid ? normalized : (value ?? "").Trim();
    }

    private static bool ValidOptionalId(string value)
    {
        return string.IsNullOrWhiteSpace(value) || IdPattern.IsMatch(value.Trim());
    }

    private static string NormalizeSingleLine(string value, int maximumLength)
    {
        string normalized = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, maximumLength);
    }
}

internal sealed class ProjectSpaceDocument
{
    public int SchemaVersion { get; internal set; }
    public List<ProjectSpace> Spaces { get; private set; }
    public bool IsFutureSchema { get; internal set; }
    internal Dictionary<string, object> UnknownFields { get; set; }
    internal string StorageRevision { get; set; }
    internal bool StorageRecoveredFromBackup { get; set; }

    public ProjectSpaceDocument()
    {
        SchemaVersion = ProjectSpaceStore.CurrentSchemaVersion;
        Spaces = new List<ProjectSpace>();
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        StorageRevision = null;
        StorageRecoveredFromBackup = false;
    }

    internal ProjectSpace Find(string id)
    {
        if (IsFutureSchema || string.IsNullOrWhiteSpace(id)) return null;
        return Spaces.Find(delegate(ProjectSpace item)
        {
            return item != null && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase);
        });
    }

    internal ProjectSpace MostRecent()
    {
        if (IsFutureSchema) return null;
        ProjectSpace result = null;
        foreach (ProjectSpace item in Spaces)
        {
            if (item == null || !item.Enabled) continue;
            if (result == null || Nullable.Compare(item.LastUsedUtc, result.LastUsedUtc) > 0)
                result = item;
        }
        return result;
    }
}

internal sealed class ProjectSpaceLoadResult
{
    public bool IsSuccess { get; private set; }
    public bool WasMigrated { get; private set; }
    public bool RecoveredFromBackup { get; private set; }
    public string ErrorCode { get; private set; }
    public ProjectSpaceDocument Document { get; private set; }

    private ProjectSpaceLoadResult() { }

    internal static ProjectSpaceLoadResult Success(ProjectSpaceDocument document,
        bool migrated, bool recovered)
    {
        return new ProjectSpaceLoadResult
        {
            IsSuccess = true,
            WasMigrated = migrated,
            RecoveredFromBackup = recovered,
            ErrorCode = "",
            Document = document ?? new ProjectSpaceDocument()
        };
    }

    internal static ProjectSpaceLoadResult Failure(string errorCode)
    {
        return new ProjectSpaceLoadResult
        {
            IsSuccess = false,
            WasMigrated = false,
            RecoveredFromBackup = false,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "PROJECT-STORE-READ-FAILED" : errorCode,
            Document = new ProjectSpaceDocument()
        };
    }
}

internal static class ProjectSpaceValidation
{
    // ChatGPT is a packaged Windows app, not a versioned editor executable.
    // Persist its stable app identity instead of a WindowsApps path.
    internal const string ChatGptAppReference = "app-id:OpenAI.Codex_2p2nqsd0c76g0!App";
    internal const string ChatGptAppId = "OpenAI.Codex_2p2nqsd0c76g0!App";
    private static readonly Regex LocalDrivePath = new Regex(@"^[A-Za-z]:\\", RegexOptions.Compiled);

    internal static bool TryNormalizeWorkspacePath(string value, out string normalized, out string errorCode)
    {
        normalized = "";
        if (!TryNormalizeLocalPath(value, true, out normalized))
        {
            errorCode = "PROJECT-WORKSPACE-INVALID";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal static bool TryNormalizeEditorExecutable(string value, string editorKind,
        out string normalized, out string errorCode)
    {
        string kind = (editorKind ?? "").Trim().ToLowerInvariant();
        if (kind == "chatgpt")
        {
            if (string.Equals((value ?? "").Trim(), ChatGptAppReference, StringComparison.OrdinalIgnoreCase) ||
                string.Equals((value ?? "").Trim(), ChatGptAppId, StringComparison.OrdinalIgnoreCase))
            {
                normalized = ChatGptAppReference;
                errorCode = "";
                return true;
            }
            normalized = "";
            errorCode = "PROJECT-CHATGPT-APP-REFERENCE-INVALID";
            return false;
        }
        if (!TryNormalizeApplicationExecutable(value, out normalized, out errorCode)) return false;
        string fileName = Path.GetFileName(normalized);
        if ((kind == "cursor" && !string.Equals(fileName, "Cursor.exe", StringComparison.OrdinalIgnoreCase)) ||
            (kind == "vscode" && !string.Equals(fileName, "Code.exe", StringComparison.OrdinalIgnoreCase)) ||
            (kind != "cursor" && kind != "vscode" && kind != "other"))
        {
            normalized = "";
            errorCode = "PROJECT-EDITOR-ADAPTER-MISMATCH";
            return false;
        }
        return true;
    }

    internal static bool TryNormalizeApplicationExecutable(string value,
        out string normalized, out string errorCode)
    {
        normalized = "";
        if (!TryNormalizeLocalPath(value, false, out normalized) ||
            !string.Equals(Path.GetExtension(normalized), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "";
            errorCode = "PROJECT-APPLICATION-INVALID";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal static bool TryNormalizePreviewUrl(string value, out string normalized, out string errorCode)
    {
        normalized = "";
        Uri uri;
        if (!TryCreateSafeHttpUri(value, out uri))
        {
            errorCode = "PROJECT-PREVIEW-URL-INVALID";
            return false;
        }
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !IsExactLoopbackHost(uri.Host))
        {
            errorCode = "PROJECT-PREVIEW-HTTPS-REQUIRED";
            return false;
        }
        normalized = uri.AbsoluteUri;
        errorCode = "";
        return true;
    }

    internal static bool TryNormalizeHttpsUrl(string value, out string normalized, out string errorCode)
    {
        normalized = "";
        Uri uri;
        if (!TryCreateSafeHttpUri(value, out uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "PROJECT-HTTPS-URL-REQUIRED";
            return false;
        }
        normalized = uri.AbsoluteUri;
        errorCode = "";
        return true;
    }

    private static bool TryNormalizeLocalPath(string value, bool directory,
        out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value)) return false;
        string candidate = value.Trim();
        if (candidate.IndexOf('%') >= 0 || ContainsControlCharacter(candidate) ||
            candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            candidate.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            candidate.StartsWith("\\\\.\\", StringComparison.Ordinal) ||
            !Path.IsPathRooted(candidate)) return false;
        try
        {
            string full = Path.GetFullPath(candidate);
            if (!LocalDrivePath.IsMatch(full) || full.StartsWith("\\\\", StringComparison.Ordinal)) return false;
            if (directory ? !Directory.Exists(full) : !File.Exists(full)) return false;
            normalized = full.TrimEnd(directory ? new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } : new char[0]);
            if (directory && normalized.Length == 2 && normalized[1] == ':') normalized += Path.DirectorySeparatorChar;
            return true;
        }
        catch { return false; }
    }

    private static bool TryCreateSafeHttpUri(string value, out Uri uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) || ContainsControlCharacter(value)) return false;
        string candidate = value.Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri) || string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.IsFile) return false;
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactLoopbackHost(string host)
    {
        string value = (host ?? "").Trim().Trim('[', ']');
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "127.0.0.1", StringComparison.OrdinalIgnoreCase)) return true;
        System.Net.IPAddress address;
        return System.Net.IPAddress.TryParse(value, out address) &&
            address.Equals(System.Net.IPAddress.IPv6Loopback);
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (char character in value)
            if (char.IsControl(character)) return true;
        return false;
    }
}

internal static class ProjectSpaceValueCopy
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
