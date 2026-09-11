using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

internal sealed class FocusTargetDescriptor
{
    private static readonly Regex IdPattern = new Regex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);
    private static readonly Regex ProcessPattern = new Regex(@"^[A-Za-z0-9._-]{1,80}$", RegexOptions.Compiled);

    public string Id { get; set; }
    public string Name { get; set; }
    public string ProcessName { get; set; }
    public string AutomationId { get; set; }
    public string ControlType { get; set; }
    public string ClassName { get; set; }
    public string ParentFingerprint { get; set; }
    public string Strategy { get; set; }
    public DateTime? LastVerifiedUtc { get; set; }
    internal Dictionary<string, object> UnknownFields { get; set; }

    public FocusTargetDescriptor()
    {
        Id = "";
        Name = "";
        ProcessName = "";
        AutomationId = "";
        ControlType = "";
        ClassName = "";
        ParentFingerprint = "";
        Strategy = "uia";
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public FocusTargetDescriptor Copy()
    {
        return new FocusTargetDescriptor
        {
            Id = Id,
            Name = Name,
            ProcessName = ProcessName,
            AutomationId = AutomationId,
            ControlType = ControlType,
            ClassName = ClassName,
            ParentFingerprint = ParentFingerprint,
            Strategy = Strategy,
            LastVerifiedUtc = LastVerifiedUtc,
            UnknownFields = new Dictionary<string, object>(UnknownFields, StringComparer.OrdinalIgnoreCase)
        };
    }

    public bool TryValidateForExecution(out string errorCode)
    {
        if (!TryValidateForVerification(out errorCode)) return false;
        if (!LastVerifiedUtc.HasValue)
        {
            errorCode = "FOCUS-TARGET-UNVERIFIED";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal bool TryValidateForVerification(out string errorCode)
    {
        if (!TryValidateForStorage(out errorCode)) return false;
        bool focusOnly = string.Equals(Strategy, "uia_focus", StringComparison.OrdinalIgnoreCase);
        if (!focusOnly && !string.Equals(Strategy, "uia", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "FOCUS-STRATEGY-UNSUPPORTED";
            return false;
        }
        // "uia_focus" targets are console/terminal text surfaces: they hold focus and
        // can be locked/restored, but the app never writes into them itself.
        if (focusOnly)
        {
            if (!string.Equals(ControlType, "Text", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ControlType, "Document", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ControlType, "Edit", StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "FOCUS-TARGET-NOT-EDITABLE";
                return false;
            }
        }
        else if (!string.Equals(ControlType, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "FOCUS-TARGET-NOT-EDITABLE";
            return false;
        }
        if (string.IsNullOrWhiteSpace(AutomationId) && string.IsNullOrWhiteSpace(ParentFingerprint))
        {
            errorCode = "FOCUS-DESCRIPTOR-UNSTABLE";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal bool TryValidateForStorage(out string errorCode)
    {
        if (!IdPattern.IsMatch((Id ?? "").Trim()))
        {
            errorCode = "FOCUS-TARGET-ID-INVALID";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 80)
        {
            errorCode = "FOCUS-TARGET-NAME-INVALID";
            return false;
        }
        string normalizedProcess = NormalizeProcessName(ProcessName);
        if (!ProcessPattern.IsMatch(normalizedProcess))
        {
            errorCode = "FOCUS-PROCESS-INVALID";
            return false;
        }
        if (string.IsNullOrWhiteSpace(ControlType) || ControlType.Trim().Length > 48)
        {
            errorCode = "FOCUS-CONTROL-TYPE-INVALID";
            return false;
        }
        if (string.IsNullOrWhiteSpace(AutomationId) && string.IsNullOrWhiteSpace(ClassName) &&
            string.IsNullOrWhiteSpace(ParentFingerprint))
        {
            errorCode = "FOCUS-DESCRIPTOR-UNSTABLE";
            return false;
        }
        if ((AutomationId ?? "").Length > 160 || (ClassName ?? "").Length > 160 ||
            (ParentFingerprint ?? "").Length > 512 || (Strategy ?? "").Length > 48)
        {
            errorCode = "FOCUS-DESCRIPTOR-TOO-LONG";
            return false;
        }
        errorCode = "";
        return true;
    }

    internal void NormalizeForStorage()
    {
        Id = (Id ?? "").Trim();
        Name = NormalizeSingleLine(Name, 80);
        ProcessName = NormalizeProcessName(ProcessName);
        AutomationId = NormalizeSingleLine(AutomationId, 160);
        ControlType = NormalizeSingleLine(ControlType, 48);
        ClassName = NormalizeSingleLine(ClassName, 160);
        ParentFingerprint = NormalizeSingleLine(ParentFingerprint, 512);
        Strategy = NormalizeSingleLine(Strategy, 48).ToLowerInvariant();
    }

    internal static string NormalizeProcessName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string candidate = value.Trim().Trim('"');
        try { candidate = Path.GetFileNameWithoutExtension(candidate); }
        catch { return ""; }
        candidate = candidate.Trim().ToLowerInvariant();
        return ProcessPattern.IsMatch(candidate) ? candidate : "";
    }

    private static string NormalizeSingleLine(string value, int maximumLength)
    {
        string normalized = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, maximumLength);
    }
}

internal sealed class FocusTargetDocument
{
    public int SchemaVersion { get; internal set; }
    public string DefaultTargetId { get; set; }
    public List<FocusTargetDescriptor> Targets { get; private set; }
    public bool IsFutureSchema { get; internal set; }
    internal Dictionary<string, object> UnknownFields { get; set; }
    internal string StorageRevision { get; set; }
    internal bool StorageRecoveredFromBackup { get; set; }

    public FocusTargetDocument()
    {
        SchemaVersion = FocusTargetStore.CurrentSchemaVersion;
        DefaultTargetId = "";
        Targets = new List<FocusTargetDescriptor>();
        UnknownFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        StorageRevision = null;
        StorageRecoveredFromBackup = false;
    }

    public FocusTargetDescriptor DefaultTarget()
    {
        if (IsFutureSchema) return null;
        if (string.IsNullOrWhiteSpace(DefaultTargetId)) return null;
        return Targets.Find(delegate(FocusTargetDescriptor target)
        {
            return target != null && string.Equals(target.Id, DefaultTargetId, StringComparison.OrdinalIgnoreCase);
        });
    }
}

internal sealed class FocusTargetLoadResult
{
    public bool IsSuccess { get; private set; }
    public bool WasMigrated { get; private set; }
    public bool RecoveredFromBackup { get; private set; }
    public string ErrorCode { get; private set; }
    public FocusTargetDocument Document { get; private set; }

    private FocusTargetLoadResult() { }

    internal static FocusTargetLoadResult Success(FocusTargetDocument document, bool migrated, bool recovered)
    {
        return new FocusTargetLoadResult
        {
            IsSuccess = true,
            WasMigrated = migrated,
            RecoveredFromBackup = recovered,
            ErrorCode = "",
            Document = document ?? new FocusTargetDocument()
        };
    }

    internal static FocusTargetLoadResult Failure(string errorCode)
    {
        return new FocusTargetLoadResult
        {
            IsSuccess = false,
            WasMigrated = false,
            RecoveredFromBackup = false,
            ErrorCode = errorCode ?? "FOCUS-STORE-READ-FAILED",
            Document = new FocusTargetDocument()
        };
    }
}
