using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Dropout frequency is the metric the stability plan promises to drive down per
// release, and it cannot be judged from a single session. This keeps a bounded,
// metadata-only history of link measurements (gap, drops, latency, duration) so the
// self-check can compare the current session with this machine's own baseline.
// No text, no clipboard content, no window titles are ever stored.
internal sealed class LinkBaselineSample
{
    public string at { get; set; }
    public int maxGapMs { get; set; }
    public int drops { get; set; }
    public int triggerToReadyMs { get; set; }
    public int audioMs { get; set; }
    public string reason { get; set; }
}

internal sealed class LinkBaselineDocument
{
    public int schemaVersion { get; set; }
    public List<LinkBaselineSample> samples { get; set; }
}

internal sealed class LinkBaselineSummary
{
    internal int Count;
    internal int AverageMaxGapMs;
    internal int WorstMaxGapMs;
    internal int OverThresholdCount;
    internal int AverageTriggerToReadyMs;

    internal bool HasData { get { return Count > 0; } }

    internal string Describe()
    {
        if (!HasData) return "";
        return "本机基线：最近 " + Count + " 次会话平均最大间隔 " + AverageMaxGapMs + " ms、最差 " +
            WorstMaxGapMs + " ms，其中 " + OverThresholdCount + " 次超过 250 ms";
    }
}

internal sealed class LinkBaselineStore
{
    internal const int Capacity = 20;
    private const int SchemaVersion = 1;
    private readonly string path;
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    internal LinkBaselineStore(string userStateRoot)
    {
        path = Path.Combine(userStateRoot, "link-baseline.json");
    }

    internal string FilePath { get { return path; } }

    internal List<LinkBaselineSample> Load()
    {
        try
        {
            if (!File.Exists(path)) return new List<LinkBaselineSample>();
            string content = File.ReadAllText(path, Encoding.UTF8);
            LinkBaselineDocument document = serializer.Deserialize<LinkBaselineDocument>(content);
            if (document == null || document.samples == null) return new List<LinkBaselineSample>();
            if (document.schemaVersion > SchemaVersion) return new List<LinkBaselineSample>();
            return document.samples;
        }
        catch
        {
            return new List<LinkBaselineSample>();
        }
    }

    internal bool TryAppend(LinkBaselineSample sample, out LinkBaselineSummary summary)
    {
        summary = Summarize(null);
        if (sample == null) return false;
        try
        {
            var samples = Load();
            samples.Add(sample);
            while (samples.Count > Capacity) samples.RemoveAt(0);
            var document = new LinkBaselineDocument { schemaVersion = SchemaVersion, samples = samples };
            string content = serializer.Serialize(document);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(temporary, path, true);
            File.Delete(temporary);
            summary = Summarize(samples);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal LinkBaselineSummary CurrentSummary()
    {
        return Summarize(Load());
    }

    private static LinkBaselineSummary Summarize(List<LinkBaselineSample> samples)
    {
        var summary = new LinkBaselineSummary();
        if (samples == null || samples.Count == 0) return summary;
        long gapTotal = 0;
        long latencyTotal = 0;
        foreach (LinkBaselineSample sample in samples)
        {
            if (sample == null) continue;
            summary.Count++;
            gapTotal += Math.Max(0, sample.maxGapMs);
            latencyTotal += Math.Max(0, sample.triggerToReadyMs);
            if (sample.maxGapMs > summary.WorstMaxGapMs) summary.WorstMaxGapMs = sample.maxGapMs;
            if (sample.maxGapMs > 250) summary.OverThresholdCount++;
        }
        if (summary.Count == 0) return summary;
        summary.AverageMaxGapMs = (int)(gapTotal / summary.Count);
        summary.AverageTriggerToReadyMs = (int)(latencyTotal / summary.Count);
        return summary;
    }
}
