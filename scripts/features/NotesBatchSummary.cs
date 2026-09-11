using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal static class NotesBatchSummaryPolicy
{
    internal const int MinimumSourceCount = 2;
    internal const int MaximumSourceCharacters = 120000;
    internal const string StaleSourceErrorCode = "NOTES-SUMMARY-SOURCE-STALE";

    internal static bool ShouldIgnoreResult(int resultGeneration, int currentGeneration,
        bool cancellationRequested)
    {
        return cancellationRequested || resultGeneration != currentGeneration;
    }

    internal static bool TryBuildSource(IEnumerable<NoteItem> selected, out string source,
        out List<NoteItem> snapshot, out string errorCode)
    {
        source = "";
        snapshot = new List<NoteItem>();
        errorCode = "";
        if (selected != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NoteItem note in selected)
            {
                if (note == null || note.IsDeleted || string.IsNullOrWhiteSpace(note.Id) ||
                    !seen.Add(note.Id)) continue;
                snapshot.Add(note.Copy());
            }
        }
        if (snapshot.Count < MinimumSourceCount)
        {
            errorCode = "NOTES-SUMMARY-NEEDS-MULTIPLE";
            return false;
        }
        var builder = new StringBuilder();
        foreach (NoteItem note in snapshot)
        {
            builder.Append("[便签 ").Append(note.Id).Append(" · 修订 ")
                .Append(note.Revision).AppendLine("]");
            builder.Append("标题：").AppendLine(note.DisplayTitle());
            builder.AppendLine(note.Body ?? "");
            builder.AppendLine("---");
            if (builder.Length > MaximumSourceCharacters)
            {
                source = "";
                errorCode = "NOTES-SUMMARY-TOO-LONG";
                return false;
            }
        }
        source = builder.ToString().Trim();
        return true;
    }

    internal static bool AreCurrent(NotesStore store, IEnumerable<NoteItem> snapshot,
        out string errorCode)
    {
        errorCode = "";
        if (store == null || snapshot == null)
        {
            errorCode = StaleSourceErrorCode;
            return false;
        }
        var expected = snapshot.Where(note => note != null && !string.IsNullOrWhiteSpace(note.Id))
            .GroupBy(note => note.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
        if (expected.Count < MinimumSourceCount)
        {
            errorCode = "NOTES-SUMMARY-NEEDS-MULTIPLE";
            return false;
        }
        NotesLoadResult loaded = store.Load();
        if (!loaded.IsSuccess || loaded.Document == null || loaded.Document.notes == null)
        {
            errorCode = string.IsNullOrWhiteSpace(loaded.ErrorCode) ?
                "NOTES-STORE-READ-FAILED" : loaded.ErrorCode;
            return false;
        }
        foreach (NoteItem item in expected)
        {
            NoteItem current = loaded.Document.notes.FirstOrDefault(note => note != null &&
                string.Equals(note.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            if (current == null || current.IsDeleted || current.Revision != item.Revision)
            {
                errorCode = StaleSourceErrorCode;
                return false;
            }
        }
        return true;
    }
}
