using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NotesAiOperationForm : Form
{
    private readonly NotesStore notesStore;
    private readonly AiProviderStore providerStore;
    private readonly AiTextService service;
    private readonly NoteItem source;
    private readonly ComboBox operation = new ComboBox();
    private readonly TextBox language = new TextBox();
    private readonly TextBox preview = new TextBox();
    private readonly Label status = new Label();
    private readonly Button run = new Button();
    private readonly Button save = new Button();
    private readonly Button copy = new Button();
    private CancellationTokenSource cancellation;
    private string resultText = "";
    private string resultModel = "";
    private AiOperationKind resultOperation;
    private bool hasResultOperation;
    private NoteCategorySuggestion categorySuggestion;
    private List<string> existingCategories = new List<string>();

    internal NotesAiOperationForm(NotesStore store, AiProviderStore providers, AiTextService textService,
        NoteItem note, Form owner)
    {
        notesStore = store;
        providerStore = providers;
        service = textService;
        source = note.Copy();
        Text = "AI 文本整理 · " + source.DisplayTitle();
        Width = 760;
        Height = 640;
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9f);
        BuildControls();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cancellation != null) cancellation.Cancel();
        base.OnFormClosing(e);
    }

    private void BuildControls()
    {
        Panel root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = Color.FromArgb(247, 248, 250) };
        Label heading = new Label { Text = "只处理你明确选择的这条便签", Dock = DockStyle.Top, Height = 32, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(25, 33, 43) };
        root.Controls.Add(heading);
        Label scope = new Label { Text = "原文不会自动覆盖；模型结果先预览，保存后作为独立整理版记录。", Dock = DockStyle.Top, Height = 28, ForeColor = Color.FromArgb(96, 106, 120) };
        root.Controls.Add(scope);
        FlowLayoutPanel choices = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        operation.DropDownStyle = ComboBoxStyle.DropDownList;
        operation.Width = 150;
        operation.Items.Add("整理表达");
        operation.Items.Add("建议分类");
        operation.Items.Add("汇总这条便签");
        operation.Items.Add("翻译");
        operation.SelectedIndex = 0;
        operation.SelectedIndexChanged += delegate
        {
            language.Enabled = operation.SelectedIndex == 3;
            categorySuggestion = null;
            resultText = "";
            hasResultOperation = false;
            preview.Text = "";
            save.Enabled = false;
            copy.Enabled = false;
            save.Text = "保存为整理版";
        };
        choices.Controls.Add(operation);
        language.Width = 150;
        language.Height = 28;
        language.Text = "目标语言，例如 English";
        language.Enabled = false;
        choices.Controls.Add(language);
        run.Text = "请求预览";
        run.Width = 104;
        run.Height = 30;
        run.Click += delegate { RunRequest(); };
        choices.Controls.Add(run);
        root.Controls.Add(choices);
        Label sourceLabel = new Label { Text = "材料范围：" + source.DisplayTitle() + " · 修订 " + source.Revision, Dock = DockStyle.Top, Height = 26, ForeColor = Color.FromArgb(96, 106, 120) };
        root.Controls.Add(sourceLabel);
        TextBox original = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Top, Height = 150, BackColor = Color.White, Text = source.Body ?? "" };
        root.Controls.Add(original);
        Label previewLabel = new Label { Text = "结果预览", Dock = DockStyle.Top, Height = 28, Padding = new Padding(0, 8, 0, 0), Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(25, 33, 43) };
        root.Controls.Add(previewLabel);
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.White;
        root.Controls.Add(preview);
        FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        save.Text = "保存为整理版";
        save.Width = 120;
        save.Height = 34;
        save.Enabled = false;
        save.Click += delegate { SaveResult(); };
        copy.Text = "复制结果";
        copy.Width = 92;
        copy.Height = 34;
        copy.Enabled = false;
        copy.Click += delegate
        {
            if (string.IsNullOrWhiteSpace(resultText)) return;
            if (!EnsureCurrentSourceForOutput()) return;
            Clipboard.SetText(resultText);
            SetStatus("结果已复制到剪贴板", Color.FromArgb(10, 164, 104));
        };
        Button cancel = new Button { Text = "关闭", Width = 78, Height = 34 };
        cancel.Click += delegate { Close(); };
        status.AutoEllipsis = true;
        status.Width = 300;
        status.Height = 34;
        status.Padding = new Padding(8, 8, 0, 0);
        status.ForeColor = Color.FromArgb(96, 106, 120);
        bottom.Controls.Add(save);
        bottom.Controls.Add(copy);
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(status);
        root.Controls.Add(bottom);
        Controls.Add(root);
    }

    private void RunRequest()
    {
        if (cancellation != null) return;
        AiProviderLoadResult loaded = providerStore.Load();
        AiProviderProfile profile = loaded.IsSuccess ? loaded.Document.Providers.FirstOrDefault(item =>
            item != null && string.Equals(item.Id, loaded.Document.DefaultProviderId, StringComparison.OrdinalIgnoreCase)) : null;
        if (profile == null && loaded.IsSuccess) profile = loaded.Document.Providers.FirstOrDefault(item => item != null && item.IsDefault);
        if (profile == null)
        {
            SetStatus("尚未配置模型；请到设置 → AI 模型添加一个配置", Color.FromArgb(229, 151, 39));
            return;
        }
        string apiKey = providerStore.ReadApiKey(profile.Id);
        AiOperationKind kind = (AiOperationKind)Math.Max(0, Math.Min(3, operation.SelectedIndex));
        string targetLanguage = language.Enabled ? language.Text : "";
        if (targetLanguage == "目标语言，例如 English") targetLanguage = "";
        NotesLoadResult currentNotes = notesStore.Load();
        existingCategories = currentNotes.IsSuccess
            ? NoteCategoryPolicy.CollectCategories(currentNotes.Document.notes)
            : new List<string> { NoteCategoryPolicy.DefaultCategory };
        cancellation = new CancellationTokenSource();
        resultText = "";
        hasResultOperation = false;
        categorySuggestion = null;
        preview.Text = "正在请求模型…";
        run.Enabled = false;
        operation.Enabled = false;
        language.Enabled = false;
        save.Enabled = false;
        copy.Enabled = false;
        SetStatus("正在请求 · " + profile.Model, Color.FromArgb(0, 127, 142));
        Task<AiTextResult> task;
        try
        {
            task = service.ExecuteAsync(profile, apiKey, new AiTextRequest
            {
                Operation = kind,
                SourceText = source.Body ?? "",
                SourceTitle = source.DisplayTitle(),
                TargetLanguage = targetLanguage,
                NoteId = source.Id,
                ExistingCategories = existingCategories
            }, cancellation.Token);
        }
        catch
        {
            cancellation.Dispose();
            cancellation = null;
            operation.Enabled = true;
            language.Enabled = operation.SelectedIndex == 3;
            run.Enabled = true;
            SetStatus("模型请求没有启动", Color.FromArgb(204, 70, 82));
            return;
        }
        task.ContinueWith(delegate(Task<AiTextResult> completed)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(delegate
            {
                cancellation.Dispose();
                cancellation = null;
                run.Enabled = true;
                operation.Enabled = true;
                language.Enabled = operation.SelectedIndex == 3;
                AiTextResult result = completed.IsCanceled ? AiTextResult.Canceled() :
                    completed.IsFaulted ? AiTextResult.Failure("模型请求异常，原文没有改变", "AI-REQUEST-FAILED") : completed.Result;
                if (result.IsCanceled)
                {
                    preview.Text = "";
                    SetStatus(result.Message, Color.FromArgb(229, 151, 39));
                    return;
                }
                if (!result.IsSuccess)
                {
                    preview.Text = "";
                    SetStatus(result.Message + " · " + result.ErrorCode, Color.FromArgb(204, 70, 82));
                    return;
                }
                if (!EnsureCurrentSourceForOutput()) return;
                resultText = result.Output;
                resultModel = result.Model;
                resultOperation = kind;
                hasResultOperation = true;
                if (kind == AiOperationKind.Classify)
                {
                    string categoryError;
                    if (!NoteCategorySuggestion.TryParse(resultText, source.Id, existingCategories,
                        out categorySuggestion, out categoryError))
                    {
                        preview.Text = resultText;
                        save.Enabled = false;
                        copy.Enabled = true;
                        SetStatus("分类建议无法验证，原文没有改变 · " + categoryError, Color.FromArgb(204, 70, 82));
                        return;
                    }
                    preview.Text = "目标分类：" + categorySuggestion.TargetCategory +
                        (string.IsNullOrWhiteSpace(categorySuggestion.Reason) ? "" : "\r\n理由：" + categorySuggestion.Reason);
                    save.Text = "确认分类";
                }
                else
                {
                    preview.Text = resultText;
                    save.Text = "保存为整理版";
                }
                save.Enabled = true;
                copy.Enabled = true;
                SetStatus(kind == AiOperationKind.Classify ? "已返回分类建议 · 等待你确认" :
                    "已返回预览 · 原文未改变", Color.FromArgb(10, 164, 104));
            }));
        }, TaskScheduler.Default);
    }

    private void SaveResult()
    {
        if (string.IsNullOrWhiteSpace(resultText)) return;
        if (!EnsureCurrentSourceForOutput()) return;
        AiOperationKind operationKind = hasResultOperation ? resultOperation :
            (AiOperationKind)Math.Max(0, Math.Min(3, operation.SelectedIndex));
        bool isClassify = operationKind == AiOperationKind.Classify;
        if (isClassify && categorySuggestion == null)
        {
            SetStatus("分类建议尚未通过校验，未修改便签", Color.FromArgb(204, 70, 82));
            return;
        }
        string error;
        NoteAiResult savedResult = new NoteAiResult
        {
            Operation = OperationKey(operationKind),
            Output = resultText,
            Model = resultModel,
            SourceRevision = source.Revision
        };
        if (!notesStore.TryUpdate(source.Id, source.Revision, delegate(NoteItem note)
        {
            if (note.AiResults == null) note.AiResults = new System.Collections.Generic.List<NoteAiResult>();
            note.AiResults.Add(savedResult);
            if (isClassify) note.Category = categorySuggestion.TargetCategory;
            return true;
        }, out error))
        {
            SetStatus(error == "NOTES-REVISION-CONFLICT" ? "原文已修改；整理结果未写入，请重新请求" : "结果没有保存：" + error,
                Color.FromArgb(204, 70, 82));
            return;
        }
        // Saving an AI result advances the note revision without changing the
        // captured source text; keep the result copy action valid for this save.
        source.Revision++;
        SetStatus(isClassify ? "分类已应用，原文保留" : "整理版已保存，原文保留", Color.FromArgb(10, 164, 104));
        save.Enabled = false;
        categorySuggestion = null;
    }

    private bool EnsureCurrentSourceForOutput()
    {
        string errorCode;
        if (NotesAiResultPolicy.IsCurrent(notesStore, source, out errorCode)) return true;
        resultText = "";
        resultModel = "";
        categorySuggestion = null;
        hasResultOperation = false;
        preview.Text = "";
        save.Enabled = false;
        copy.Enabled = false;
        string message = errorCode == NotesAiResultPolicy.StaleNoteErrorCode
            ? "便签已修改或删除，迟到结果未显示，也未执行复制或保存"
            : "便签当前无法读取，迟到结果未显示，也未执行复制或保存";
        SetStatus(message + " · " + errorCode, Color.FromArgb(204, 70, 82));
        return false;
    }

    private string OperationKey(AiOperationKind kind)
    {
        switch (kind)
        {
            case AiOperationKind.Classify: return "classify";
            case AiOperationKind.Summarize: return "summarize";
            case AiOperationKind.Translate: return "translate";
            default: return "organize";
        }
    }

    private void SetStatus(string text, Color color)
    {
        status.Text = text ?? "";
        status.ForeColor = color;
    }
}
