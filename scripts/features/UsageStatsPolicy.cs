using System;
using System.Collections.Generic;

// Usage statistics behind the local-first story: how often dictation ran, how often it finished
// cleanly, and the longest gap between two runs.
//
// Everything here is derived from the runtime receipt log the Host already writes, and only timestamps
// and boolean outcome markers are ever read. No target name, window title, device address or
// transcription text passes through this policy, and it stores nothing: the statistics panel therefore
// adds no personal data of its own and cannot leak text it never sees.
//
// This file is deliberately ASCII-only. It is written without a byte-order mark, and the C# compiler
// falls back to the ANSI code page for BOM-less sources, so a non-ASCII literal here would be mangled.
// All user-facing text is formatted by the Host instead.
internal sealed class UsageStats
{
    public int Sessions { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public long LongestGapMs { get; set; }
    public DateTime LastSessionLocal { get; set; }

    internal UsageStats()
    {
        LastSessionLocal = DateTime.MinValue;
    }

    internal bool HasHistory { get { return Sessions > 0; } }

    internal double SuccessRatePercent
    {
        get { return Sessions <= 0 ? 0.0 : Succeeded * 100.0 / Sessions; }
    }
}

internal static class UsageStatsPolicy
{
    // Session-end receipts the Host already writes. Hold-to-talk and the long-dictation path each emit
    // one, so a completed attempt is counted the same way on both.
    internal const string WeTypeSessionEnd = "WETYPE SESSION END";
    internal const string TranscriptionSessionEnd = "TRANSCRIPTION SESSION END";
    internal const string SessionError = "SESSION ERROR";
    internal const string AudioDeliveredTrue = "audio_delivered=True";
    internal const string SubmittedTrue = "submitted=True";

    // A run is only clean when the receipt says the audio reached the tool AND the tool submitted it.
    // Anything else counts as not clean, so the rate can never flatter itself.
    internal static bool IsCleanRun(string line)
    {
        if (line == null) return false;
        if (line.IndexOf(SessionError, StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return line.IndexOf(AudioDeliveredTrue, StringComparison.OrdinalIgnoreCase) >= 0 &&
            line.IndexOf(SubmittedTrue, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool IsSessionEnd(string line)
    {
        if (line == null) return false;
        return line.IndexOf(WeTypeSessionEnd, StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(TranscriptionSessionEnd, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Aggregates receipt lines. Only ended sessions are counted, and only lines whose timestamp parses
    // can contribute to the gap, so a log window that lost its header cannot invent a bogus interval.
    internal static UsageStats Summarize(IList<string> lines, Func<string, DateTime?> timestampOf)
    {
        var stats = new UsageStats();
        if (lines == null) return stats;
        DateTime previousEnd = DateTime.MinValue;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            if (!IsSessionEnd(line)) continue;
            stats.Sessions++;
            if (IsCleanRun(line)) stats.Succeeded++;
            else stats.Failed++;

            DateTime? stamp = timestampOf == null ? null : timestampOf(line);
            if (!stamp.HasValue) continue;
            DateTime ended = stamp.Value;
            if (ended > stats.LastSessionLocal) stats.LastSessionLocal = ended;
            if (previousEnd != DateTime.MinValue)
            {
                long gap = (long)(ended - previousEnd).TotalMilliseconds;
                if (gap > stats.LongestGapMs) stats.LongestGapMs = gap;
            }
            previousEnd = ended;
        }
        return stats;
    }
}
