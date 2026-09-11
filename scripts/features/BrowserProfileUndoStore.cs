using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class BrowserRemoteUndoLoadResult
{
    public BrowserRemoteUndoSnapshot Snapshot { get; private set; }
    public string ErrorCode { get; private set; }
    public bool UsedBackup { get; private set; }

    internal static BrowserRemoteUndoLoadResult Success(BrowserRemoteUndoSnapshot snapshot, bool usedBackup)
    {
        return new BrowserRemoteUndoLoadResult
        {
            Snapshot = snapshot,
            ErrorCode = "",
            UsedBackup = usedBackup
        };
    }

    internal static BrowserRemoteUndoLoadResult Failure(string errorCode)
    {
        return new BrowserRemoteUndoLoadResult
        {
            Snapshot = null,
            ErrorCode = errorCode ?? "BROWSER-UNDO-INVALID",
            UsedBackup = false
        };
    }
}

internal sealed class BrowserRemoteUndoStore
{
    private readonly object sync = new object();
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    public string Path { get; private set; }
    public string BackupPath { get { return Path + ".bak"; } }

    internal BrowserRemoteUndoStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root");
        Path = System.IO.Path.Combine(root, "browser-remote-undo.json");
    }

    internal BrowserRemoteUndoLoadResult Load()
    {
        lock (sync)
        {
            BrowserRemoteUndoLoadResult primary = LoadPath(Path, false);
            if (primary.Snapshot != null || primary.ErrorCode == "BROWSER-UNDO-SCHEMA-NEWER")
                return primary;
            BrowserRemoteUndoLoadResult backup = LoadPath(BackupPath, true);
            return backup.Snapshot != null ? backup : primary;
        }
    }

    internal bool Save(BrowserRemoteUndoSnapshot snapshot)
    {
        string errorCode;
        if (snapshot == null || !snapshot.TryValidate(out errorCode)) return false;
        lock (sync)
        {
            if (File.Exists(Path))
            {
                BrowserRemoteUndoLoadResult existing = LoadPath(Path, false);
                if (existing.ErrorCode == "BROWSER-UNDO-SCHEMA-NEWER") return false;
            }
            string directory = System.IO.Path.GetDirectoryName(Path);
            string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, serializer.Serialize(snapshot), Encoding.UTF8);
                if (File.Exists(Path)) File.Replace(temporary, Path, BackupPath);
                else File.Move(temporary, Path);
                return true;
            }
            catch { return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }
    }

    internal bool Clear()
    {
        lock (sync)
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                return true;
            }
            catch { return false; }
        }
    }

    private BrowserRemoteUndoLoadResult LoadPath(string path, bool usedBackup)
    {
        if (!File.Exists(path)) return BrowserRemoteUndoLoadResult.Failure("BROWSER-UNDO-MISSING");
        try
        {
            BrowserRemoteUndoSnapshot snapshot = serializer.Deserialize<BrowserRemoteUndoSnapshot>(
                File.ReadAllText(path, Encoding.UTF8));
            if (snapshot == null) return BrowserRemoteUndoLoadResult.Failure("BROWSER-UNDO-INVALID");
            string errorCode;
            if (!snapshot.TryValidate(out errorCode))
                return BrowserRemoteUndoLoadResult.Failure(errorCode);
            return BrowserRemoteUndoLoadResult.Success(snapshot, usedBackup);
        }
        catch { return BrowserRemoteUndoLoadResult.Failure("BROWSER-UNDO-MALFORMED"); }
    }
}
