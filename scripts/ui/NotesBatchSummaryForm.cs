using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NotesBatchSummaryForm : Form
{
    private readonly NotesStore notesStore;
    private readonly AiProviderStore providerStore;
    private readonly AiTextService service;
    private readonly List<NoteItem> sources;
    private readonly TextBox preview = new TextBox();
    private readonly Label status = new Label();
    private readonly Button run = new Button();
    private readonly Button save = new Button();
    private readonly Button copy = new Button();
    private readonly Button cancel = new Button();
    private CancellationTokenSource cancellation;
    private int requestGeneration;
    private string resultText = "";
    private string resultModel = "";
    private string providerId = "";

    internal NotesBatchSummaryForm(NotesStore store, AiProviderStore providers,
        AiTextService textService, IEnumerable<NoteItem> selected, Form owner)
    {
        notesStore = store;
        providerStore = providers;
        service = textService;
        sources = (selected ?? Enumerable.Empty<NoteItem>())
            .Where(note => note != null).Select(note => note.Copy()).ToList();
        Text = "AI 汇总 · 已选 " + sources.Count + " 条便签";
        Width = 780;
        Height = 680;
        MinimumSize = new Size(660, 560);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9f);
        BuildControls();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        requestGeneration++;
        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation = null;
        }
        base.OnFormClosing(e);
    }

    private void BuildControls()
    {
        Panel root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = Color.FromArgb(247, 248, 250) };
        Label heading = new Label
        {
            Text = "汇总已选便签",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 33, 43)
        };
        Label scope = new Label
        {
            Text = "只发送你明确勾选的便签；原文不会覆盖，结果保存为一条新的本地便签。",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.FromArgb(96, 106, 120)
        };
        TextBox selected = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Top,
            Height = 118,
            BackColor = Color.White,
            AccessibleName = "汇总来源便签列表",
            Text = string.Join("\r\n", sources.Select((note, index) =>
                (index + 1) + ". " + note.DisplayTitle() + " · 修订 " + note.Revision))
        };
        Label resultLabel = new Label
        {
            Text = "结果预览",
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 8, 0, 0),
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 33, 43)
        };
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.White;
        preview.AccessibleName = "汇总结果预览";
        FlowLayoutPanel actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };
        run.Text = "请求汇总";
        run.Width = 104;
        run.Height = 34;
        run.Click += delegate { RunRequest(); };
        save.Text = "保存为新便签";
        save.Width = 118;
        save.Height = 34;
        save.Enabled = false;
        save.Click += delegate { SaveResult(); };
        copy.Text = "复制结果";
        copy.Width = 92;
        copy.Height = 34;
        copy.Enabled = false;
        copy.Click += delegate
        {
            if (string.IsNullOrWhiteSpace(resultText) || !EnsureSourcesCurrent()) return;
            try
            {
                Clipboard.SetText(resultText);
                SetStatus("结果已复制到剪贴板，未发送到其他 APP", Color.FromArgb(10, 164, 104));
            }
            catch { SetStatus("结果未复制 · 剪贴板不可用", Color.FromArgb(204, 70, 82)); }
        };
        cancel.Text = "取消";
        cancel.Width = 78;
        cancel.Height = 34;
        cancel.Click += delegate
        {
            if (cancellation != null)
            {
                requestGeneration++;
                cancellation.Cancel();
                SetStatus("正在取消，本次不会自动重试", Color.FromArgb(229, 151, 39));
                return;
            }
            Close();
        };
        status.AutoEllipsis = true;
        status.Width = 300;
        status.Height = 34;
        status.Padding = new Padding(8, 8, 0, 0);
        status.ForeColor = Color.FromArgb(96, 106, 120);
        actions.Controls.Add(run);
        actions.Controls.Add(save);
        actions.Controls.Add(copy);
        actions.Controls.Add(cancel);
        actions.Controls.Add(status);
        // WinForms lays docked controls out in reverse z-order. Add these
        // top-docked controls in reverse reading order for a stable flow:
        // heading, scope, selected sources, then result preview.
        root.Controls.Add(resultLabel);
        root.Controls.Add(selected);
        root.Controls.Add(scope);
        root.Controls.Add(heading);
        root.Controls.Add(preview);
        root.Controls.Add(actions);
        Controls.Add(root);
    }

    private void RunRequest()
    {
        if (cancellation != null) return;
        string sourceText;
        List<NoteItem> snapshot;
        string sourceError;
        if (!NotesBatchSummaryPolicy.TryBuildSource(sources, out sourceText, out snapshot, out sourceError))
        {
            SetStatus(sourceError == "NOTES-SUMMARY-TOO-LONG" ?
                "所选便签内容过长，请减少选择后重试" : "至少选择两条未删除便签", Color.FromArgb(229, 151, 39));
            return;
        }
        if (!NotesBatchSummaryPolicy.AreCurrent(notesStore, snapshot, out sourceError))
        {
            SetStatus("来源便签已修改或删除，本次未发送 · " + sourceError, Color.FromArgb(204, 70, 82));
            return;
        }
        AiProviderLoadResult loaded = providerStore.Load();
        AiProviderProfile profile = loaded.IsSuccess ? loaded.Document.Providers.FirstOrDefault(item =>
            item != null && string.Equals(item.Id, loaded.Document.DefaultProviderId, StringComparison.OrdinalIgnoreCase)) : null;
        if (profile == null && loaded.IsSuccess)
            profile = loaded.Document.Providers.FirstOrDefault(item => item != null && item.IsDefault);
        if (profile == null)
        {
            SetStatus("尚未配置模型；请到设置 → AI 模型添加一个配置", Color.FromArgb(229, 151, 39));
            return;
        }
        string apiKey = providerStore.ReadApiKey(profile.Id);
        CancellationTokenSource requestCancellation = new CancellationTokenSource();
        CancellationToken requestToken = requestCancellation.Token;
        int generation = ++requestGeneration;
        cancellation = requestCancellation;
        providerId = profile.Id;
        resultText = "";
        resultModel = "";
        preview.Text = "正在请求模型…";
        run.Enabled = false;
        save.Enabled = false;
        copy.Enabled = false;
        SetStatus("正在请求 · " + profile.Model + " · 已选 " + snapshot.Count + " 条", Color.FromArgb(0, 127, 142));
        Task<AiTextResult> task;
        try
        {
            task = service.ExecuteAsync(profile, apiKey, new AiTextRequest
            {
                Operation = AiOperationKind.Summarize,
                SourceText = sourceText,
                SourceTitle = "已选便签汇总",
                SourceNoteIds = snapshot.Select(note => note.Id).ToList(),
                SourceRevisions = snapshot.Select(note => note.Revision).ToList()
            }, requestToken);
        }
        catch
        {
            requestCancellation.Dispose();
            cancellation = null;
            run.Enabled = true;
            SetStatus("模型请求没有启动 · 原文没有改变", Color.FromArgb(204, 70, 82));
            return;
        }
        task.ContinueWith(delegate(Task<AiTextResult> completed)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    bool wasCanceled = requestToken.IsCancellationRequested;
                    bool ignoreResult = NotesBatchSummaryPolicy.ShouldIgnoreResult(
                        generation, requestGeneration, wasCanceled);
                    if (Object.ReferenceEquals(cancellation, requestCancellation))
                    {
                        cancellation.Dispose();
                        cancellation = null;
                    }
                    run.Enabled = true;
                    if (ignoreResult)
                    {
                        preview.Text = "";
                        save.Enabled = false;
                        copy.Enabled = false;
                        SetStatus("模型请求已取消，迟到结果未显示，也未自动重试", Color.FromArgb(229, 151, 39));
                        return;
                    }
                    AiTextResult result = completed.IsCanceled ? AiTextResult.Canceled() :
                        completed.IsFaulted ? AiTextResult.Failure("模型请求异常，原文没有改变", "AI-REQUEST-FAILED") : completed.Result;
                    if (result.IsCanceled)
                    {
                        preview.Text = "";
                        SetStatus("模型请求已取消，未自动重试", Color.FromArgb(229, 151, 39));
                        return;
                    }
                    if (!result.IsSuccess)
                    {
                        preview.Text = "";
                        SetStatus(result.Message + " · " + result.ErrorCode, Color.FromArgb(204, 70, 82));
                        return;
                    }
                    string error;
                    if (!NotesBatchSummaryPolicy.AreCurrent(notesStore, sources, out error))
                    {
                        preview.Text = "";
                        SetStatus("来源便签已修改或删除，迟到结果未显示 · " + error, Color.FromArgb(204, 70, 82));
                        return;
                    }
                    resultText = result.Output;
                    resultModel = result.Model;
                    preview.Text = resultText;
                    save.Enabled = true;
                    copy.Enabled = true;
                    SetStatus("已返回预览 · 原便签未改变，等待你确认保存", Color.FromArgb(10, 164, 104));
                }));
            }
            catch (InvalidOperationException) { }
        }, TaskScheduler.Default);
    }

    private void SaveResult()
    {
        if (string.IsNullOrWhiteSpace(resultText) || !EnsureSourcesCurrent()) return;
        NotesLoadResult loaded = notesStore.Load();
        if (!loaded.IsSuccess || loaded.Document == null)
        {
            SetStatus("汇总未保存 · 便签文件无法读取", Color.FromArgb(204, 70, 82));
            return;
        }
        string error;
        if (!NotesBatchSummaryPolicy.AreCurrent(notesStore, sources, out error))
        {
            SetStatus("来源便签已修改或删除，汇总未保存 · " + error, Color.FromArgb(204, 70, 82));
            return;
        }
        NoteItem summary = new NoteItem
        {
            Title = "汇总 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Body = resultText,
            Category = NoteCategoryPolicy.DefaultCategory
        };
        summary.AiResults.Add(new NoteAiResult
        {
            Operation = "summarize",
            Output = resultText,
            Model = resultModel,
            SourceNoteIds = sources.Select(note => note.Id).ToList(),
            SourceRevisions = sources.Select(note => note.Revision).ToList(),
            PromptVersion = "v2-notes-1",
            ProviderId = providerId,
            Status = "saved"
        });
        loaded.Document.notes.Add(summary);
        if (!notesStore.TrySave(loaded.Document, out error))
        {
            SetStatus("汇总未保存 · 原便签没有改变 · " + error, Color.FromArgb(204, 70, 82));
            return;
        }
        save.Enabled = false;
        SetStatus("汇总已保存为新便签，原便签未改变", Color.FromArgb(10, 164, 104));
    }

    private bool EnsureSourcesCurrent()
    {
        string error;
        if (NotesBatchSummaryPolicy.AreCurrent(notesStore, sources, out error)) return true;
        resultText = "";
        preview.Text = "";
        save.Enabled = false;
        copy.Enabled = false;
        SetStatus("来源便签已修改或删除，结果未显示或保存 · " + error, Color.FromArgb(204, 70, 82));
        return false;
    }

    private void SetStatus(string text, Color color)
    {
        status.Text = text ?? "";
        status.ForeColor = color;
    }
}
