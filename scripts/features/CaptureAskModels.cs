using System;
using System.Collections.Generic;
using System.Drawing;

internal delegate ActionResult CaptureAskFocusExecutor(FocusTargetDescriptor target, int timeoutMs);
internal delegate bool CaptureAskFocusVerifier(FocusTargetDescriptor target);

internal interface ICaptureAskImageClipboard
{
    bool TrySetImage(Bitmap image, out string errorCode);
}

internal interface ICaptureAskPasteDispatcher
{
    bool TryPasteImage(out string errorCode);
}

internal sealed class CaptureAskPreparedImage : IDisposable
{
    private readonly object sync = new object();
    private Bitmap image;

    public string Id { get; private set; }
    public string SourceKind { get; private set; }
    public string TempPath { get; private set; }

    internal CaptureAskPreparedImage(string id, string sourceKind, string tempPath, Bitmap image)
    {
        Id = id ?? "";
        SourceKind = sourceKind ?? "";
        TempPath = tempPath ?? "";
        this.image = image == null ? null : new Bitmap(image);
    }

    internal bool IsAvailable
    {
        get
        {
            lock (sync) return image != null;
        }
    }

    public Bitmap CreateImageCopy()
    {
        lock (sync)
        {
            if (image == null) throw new ObjectDisposedException("CaptureAskPreparedImage");
            return new Bitmap(image);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (image != null) image.Dispose();
            image = null;
        }
    }
}

internal sealed class CaptureAskPrepareResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorCode { get; private set; }
    public CaptureAskPreparedImage Capture { get; private set; }

    private CaptureAskPrepareResult() { }

    internal static CaptureAskPrepareResult Success(CaptureAskPreparedImage capture)
    {
        return new CaptureAskPrepareResult
        {
            IsSuccess = true,
            ErrorCode = "",
            Capture = capture
        };
    }

    internal static CaptureAskPrepareResult Failure(string errorCode)
    {
        return new CaptureAskPrepareResult
        {
            IsSuccess = false,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "CAPTURE-ASK-PREPARE-FAILED" : errorCode,
            Capture = null
        };
    }
}

internal sealed class CaptureAskTargetResolution
{
    public FocusTargetDescriptor Target { get; private set; }
    public bool UsedDefault { get; private set; }
    public string ErrorCode { get; private set; }

    private CaptureAskTargetResolution() { }

    internal static CaptureAskTargetResolution Success(FocusTargetDescriptor target, bool usedDefault)
    {
        return new CaptureAskTargetResolution
        {
            Target = target == null ? null : target.Copy(),
            UsedDefault = usedDefault,
            ErrorCode = ""
        };
    }

    internal static CaptureAskTargetResolution Failure(string errorCode)
    {
        return new CaptureAskTargetResolution
        {
            Target = null,
            UsedDefault = false,
            ErrorCode = errorCode ?? "CAPTURE-ASK-TARGET-MISSING"
        };
    }
}

internal static class CaptureAskTargetResolver
{
    internal static CaptureAskTargetResolution Resolve(FocusTargetDocument focusDocument,
        ProjectSpaceDocument projectDocument, string currentProjectSpaceId)
    {
        if (focusDocument == null || focusDocument.IsFutureSchema)
            return CaptureAskTargetResolution.Failure("CAPTURE-ASK-FOCUS-CONFIG-UNAVAILABLE");

        string requestedTargetId = "";
        bool explicitProjectTarget = false;
        if (!string.IsNullOrWhiteSpace(currentProjectSpaceId))
        {
            ProjectSpace project = projectDocument == null || projectDocument.IsFutureSchema
                ? null : projectDocument.Find(currentProjectSpaceId);
            if (project == null)
                return CaptureAskTargetResolution.Failure("CAPTURE-ASK-PROJECT-MISSING");
            requestedTargetId = (project.CaptureTargetId ?? "").Trim();
            explicitProjectTarget = requestedTargetId.Length > 0;
        }
        if (!explicitProjectTarget) requestedTargetId = (focusDocument.DefaultTargetId ?? "").Trim();
        if (requestedTargetId.Length == 0)
            return CaptureAskTargetResolution.Failure("FOCUS-TARGET-MISSING");

        FocusTargetDescriptor target = focusDocument.Targets.Find(delegate(FocusTargetDescriptor item)
        {
            return item != null && string.Equals(item.Id, requestedTargetId,
                StringComparison.OrdinalIgnoreCase);
        });
        if (target == null)
            return CaptureAskTargetResolution.Failure(explicitProjectTarget
                ? "CAPTURE-ASK-PROJECT-TARGET-MISSING" : "FOCUS-TARGET-MISSING");
        string validationCode;
        if (!target.TryValidateForExecution(out validationCode))
            return CaptureAskTargetResolution.Failure(explicitProjectTarget
                ? "CAPTURE-ASK-PROJECT-TARGET-UNVERIFIED" : validationCode);
        return CaptureAskTargetResolution.Success(target, !explicitProjectTarget);
    }

    internal static IList<FocusTargetDescriptor> VerifiedTargets(FocusTargetDocument document)
    {
        var results = new List<FocusTargetDescriptor>();
        if (document == null || document.IsFutureSchema) return results;
        foreach (FocusTargetDescriptor target in document.Targets)
        {
            string errorCode;
            if (target != null && target.TryValidateForExecution(out errorCode)) results.Add(target.Copy());
        }
        return results;
    }
}
