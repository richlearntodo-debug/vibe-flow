using System;
using System.Collections.Generic;

// Runs the action sequence a gesture resolved to. One gesture can therefore drive a short
// macro: steps run in order, execution stops at the first step that reports failure, and
// the outcome is reported honestly (how many ran, which one failed, why) so the UI can say
// what actually happened instead of claiming the whole macro worked.
internal sealed class GestureRunResult
{
    public bool Succeeded { get; set; }
    public int Executed { get; set; }
    public int FailedStep { get; set; }
    public string Error { get; set; }

    internal GestureRunResult()
    {
        Error = "";
        FailedStep = -1;
    }
}

internal static class GestureMacroRunner
{
    internal const string NoActionCode = "GESTURE-NO-ACTION";
    internal const string StepFailedCode = "GESTURE-STEP-FAILED";

    internal static GestureRunResult RunSteps(IList<string> steps, Func<string, bool> execute)
    {
        var result = new GestureRunResult();
        if (steps == null || steps.Count == 0)
        {
            result.Error = NoActionCode;
            return result;
        }
        if (execute == null)
        {
            result.Error = StepFailedCode;
            result.FailedStep = 0;
            return result;
        }
        for (int index = 0; index < steps.Count; index++)
        {
            string step = steps[index];
            bool ok;
            try
            {
                ok = execute(step);
            }
            catch
            {
                ok = false;
            }
            if (!ok)
            {
                result.FailedStep = index;
                result.Error = StepFailedCode;
                return result;
            }
            result.Executed = index + 1;
        }
        result.Succeeded = true;
        return result;
    }

    // One line per gesture for the log: "GESTURE MACRO steps=3 executed=2 failed=1 error=…".
    internal static string DescribeResult(GestureRunResult result)
    {
        if (result == null) return "GESTURE MACRO result=missing";
        return "GESTURE MACRO steps=" + (result.Executed + (result.Succeeded ? 0 : (result.FailedStep >= 0 ? 1 : 0))) +
            " executed=" + result.Executed +
            " failed=" + (result.FailedStep >= 0 ? result.FailedStep + 1 : 0) +
            " error=" + (string.IsNullOrEmpty(result.Error) ? "none" : result.Error);
    }
}
