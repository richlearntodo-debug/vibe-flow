using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class VibeMicForm
{
    private string activeNoteId = "";
    private bool notesShowDeleted;
    private TextBox notesSearchBox;
    private Panel notesResultsHost;
    private Panel notesPreviewHost;
    private SplitContainer notesSplitView;
    private ContextMenuStrip notesMoreMenu;
    private readonly HashSet<string> notesSelectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Button notesSummarizeButton;

    private void AddRecentNoteOverview(Point location, Size size)
    {
        NotesLoadResult loaded = notesStore.Load();
        NoteItem recent = loaded.IsSuccess && loaded.Document.notes != null
            ? loaded.Document.notes.Where(note => note != null && !note.IsDeleted)
                .OrderByDescending(note => note.UpdatedAtUtc).FirstOrDefault() : null;
        RoundPanel card = NewCard(location, size);
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Label title = NewLabel("最近便签", 11f, FontStyle.Bold, ink);
        title.Location = new Point(24, 15);
        title.Size = new Size(200, 28);
        Label detail = NewLabel(recent == null ? "还没有便签；先保存一条本地记录" :
            recent.DisplayTitle() + "  ·  " + recent.UpdatedAtUtc.ToLocalTime().ToString("MM-dd HH:mm"),
            9f, FontStyle.Regular, recent == null ? muted : green);
        detail.Location = new Point(24, 47);
        detail.Size = new Size(690, 26);
        Button action = PrimaryButton(recent == null ? "新建便签" : "打开便签",
            new Point(816, 25), new Size(120, 42));
        action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        action.Click += delegate
        {
            if (recent == null) CreateNoteAndEdit();
            else ShowNoteEditor(recent.Id, false);
        };
        card.Controls.Add(title);
        card.Controls.Add(detail);
        card.Controls.Add(action);
        content.Controls.Add(card);
    }

    private void BuildNotesPage()
    {
        NotesLoadResult loaded = notesStore.Load();
        AddPageTitle("便签", "像 Notion 一样整理本地 Markdown 和 TXT；内容只保存在本机");

        Panel toolbar = new Panel();
        toolbar.Location = new Point(34, 88);
        toolbar.Size = new Size(960, 94);
        toolbar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        toolbar.BackColor = Color.Transparent;

        TextBox search = new TextBox();
        notesSearchBox = search;
        search.Name = "notesSearchBox";
        search.Location = new Point(0, 5);
        search.Size = new Size(260, 36);
        search.Font = new Font("Microsoft YaHei UI", 10f);
        search.AccessibleName = "搜索便签标题、正文或分类";
        ToolTip searchToolTip = new ToolTip();
        searchToolTip.SetToolTip(search, "搜索便签标题、正文或分类");
        const string searchHint = "搜索便签标题、正文或分类";
        bool showingSearchHint = string.IsNullOrWhiteSpace(notesSearchText);
        search.Text = showingSearchHint ? searchHint : notesSearchText;
        search.ForeColor = showingSearchHint ? muted : ink;
        search.GotFocus += delegate
        {
            if (!showingSearchHint) return;
            showingSearchHint = false;
            search.Text = "";
            search.ForeColor = ink;
        };
        search.LostFocus += delegate
        {
            if (!string.IsNullOrWhiteSpace(search.Text)) return;
            showingSearchHint = true;
            search.Text = searchHint;
            search.ForeColor = muted;
        };
        search.TextChanged += delegate
        {
            if (showingSearchHint) return;
            notesSearchText = search.Text;
            RefreshNotesResults();
        };

        Button create = PrimaryButton("新建便签", new Point(272, 2), new Size(100, 42));
        create.Name = "createNoteButton";
        create.Click += delegate { if (!GuardNotesOperation("新建便签")) CreateNoteAndEdit(); };
        Button exportMarkdown = SecondaryButton("导出 Markdown", new Point(380, 2), new Size(140, 42));
        exportMarkdown.Name = "exportMarkdownNotesButton";
        exportMarkdown.AccessibleName = "导出 Markdown 文件";
        exportMarkdown.Click += delegate { if (!GuardNotesOperation("导出 Markdown")) ExportNotes(true); };
        Button exportText = SecondaryButton("导出 TXT", new Point(528, 2), new Size(100, 42));
        exportText.Name = "exportTextNotesButton";
        exportText.AccessibleName = "导出 TXT 文件";
        exportText.Click += delegate { if (!GuardNotesOperation("导出 TXT")) ExportNotes(false); };
        Button recycle = SecondaryButton(notesShowDeleted ? "返回收件箱" : "回收站", new Point(636, 2), new Size(112, 42));
        recycle.Name = "notesRecycleButton";
        recycle.Click += delegate
        {
            if (GuardNotesOperation("切换回收站")) return;
            notesShowDeleted = !notesShowDeleted;
            ShowPage((int)VibePageId.Notes);
        };
        notesSummarizeButton = SecondaryButton("汇总已选", new Point(756, 2), new Size(88, 42));
        notesSummarizeButton.Name = "summarizeSelectedNotesButton";
        notesSummarizeButton.AccessibleName = "汇总已选便签";
        notesSummarizeButton.Enabled = false;
        notesSummarizeButton.Click += delegate { OpenBatchSummary(); };
        toolbar.Controls.Add(search);
        toolbar.Controls.Add(create);
        toolbar.Controls.Add(exportMarkdown);
        toolbar.Controls.Add(exportText);
        toolbar.Controls.Add(recycle);
        toolbar.Controls.Add(notesSummarizeButton);
        Button more = SecondaryButton("更多", new Point(852, 2), new Size(82, 42));
        more.Name = "notesMoreButton";
        more.AccessibleName = "更多便签操作（JSON 备份、恢复、Deck）";
        EnsureNotesMoreMenu();
        more.Click += delegate { notesMoreMenu.Show(more, new Point(0, more.Height)); };
        toolbar.Controls.Add(more);
        Label toolbarHint = NewLabel("本地便签 · 自动保存 · 选择条目后在右侧预览 · 更多操作在右上角", 8.2f, FontStyle.Regular, muted);
        toolbarHint.Location = new Point(2, 52);
        toolbarHint.Size = new Size(620, 24);
        toolbar.Controls.Add(toolbarHint);
        content.Controls.Add(toolbar);

        notesSplitView = new SplitContainer
        {
            Name = "notesSplitView",
            Location = new Point(34, 192),
            Size = new Size(960, 440),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false,
            SplitterWidth = 8,
            Panel1MinSize = 280,
            Panel2MinSize = 360,
            BackColor = line
        };
        int listPaneWidth;
        int previewPaneWidth;
        NotesPageLayoutPolicy.GetPaneWidths(notesSplitView.Width, out listPaneWidth, out previewPaneWidth);
        notesSplitView.SplitterDistance = listPaneWidth;
        notesResultsHost = new Panel
        {
            Name = "notesResultsHost",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        notesPreviewHost = new Panel
        {
            Name = "notesPreviewHost",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        notesSplitView.Panel1.Controls.Add(notesResultsHost);
        notesSplitView.Panel2.Controls.Add(notesPreviewHost);
        content.Controls.Add(notesSplitView);
        RenderNotesResults(loaded);
    }

    private void EnsureNotesMoreMenu()
    {
        if (notesMoreMenu != null && !notesMoreMenu.IsDisposed) return;
        notesMoreMenu = new ContextMenuStrip();
        ToolStripMenuItem backup = new ToolStripMenuItem("备份 JSON") { Name = "backupNotesMenuItem" };
        backup.Click += delegate { if (!GuardNotesOperation("备份便签")) BackupNotesJson(); };
        ToolStripMenuItem restore = new ToolStripMenuItem("从 JSON 恢复") { Name = "restoreNotesMenuItem" };
        restore.Click += delegate { if (!GuardNotesOperation("恢复便签")) RestoreNotesJson(); };
        ToolStripMenuItem deck = new ToolStripMenuItem("打开便签 Deck") { Name = "notesDeckMenuItem" };
        deck.Click += delegate { OpenNotesDeck(); };
        ToolStripMenuItem summarize = new ToolStripMenuItem("汇总已选便签") { Name = "summarizeNotesMenuItem" };
        summarize.Click += delegate { OpenBatchSummary(); };
        notesMoreMenu.Items.Add(backup);
        notesMoreMenu.Items.Add(restore);
        notesMoreMenu.Items.Add(new ToolStripSeparator());
        notesMoreMenu.Items.Add(deck);
        notesMoreMenu.Items.Add(summarize);
    }

    private void RefreshNotesResults()
    {
        if (notesResultsHost == null || notesResultsHost.IsDisposed) return;
        RenderNotesResults(notesStore.Load());
    }

    private void RenderNotesResults(NotesLoadResult loaded)
    {
        if (notesResultsHost == null || notesResultsHost.IsDisposed) return;
        if (notesPreviewHost != null && !notesPreviewHost.IsDisposed)
            ClearNotesPreview();
        while (notesResultsHost.Controls.Count > 0)
        {
            Control old = notesResultsHost.Controls[0];
            notesResultsHost.Controls.RemoveAt(0);
            DisposeOwnedControlResources(old);
            old.Dispose();
        }
        if (loaded == null || !loaded.IsSuccess)
        {
            int paneWidth = Math.Max(280, notesResultsHost.ClientSize.Width);
            RoundPanel problem = NewCard(new Point(0, 0), new Size(paneWidth - 12, 220));
            problem.BorderColor = coral;
            Label title = NewLabel("便签暂时不可用", 12f, FontStyle.Bold, coral);
            title.Location = new Point(24, 22);
            title.Size = new Size(Math.Max(120, paneWidth - 48), 32);
            Label detail = NewLabel("发生了什么：本地便签文件无法读取。\r\n可能原因：文件损坏或用户数据目录不可访问。\r\n本次影响：没有执行编辑、删除或导出。\r\n错误码：" +
                (loaded == null ? "NOTES-STORE-READ-FAILED" : loaded.ErrorCode), 9.2f, FontStyle.Regular, muted);
            detail.Location = new Point(24, 64);
            detail.Size = new Size(Math.Max(120, paneWidth - 48), 90);
            Button retry = PrimaryButton("重新检测", new Point(24, 166), new Size(112, 40));
            retry.Click += delegate { RefreshNotesResults(); };
            problem.Controls.Add(title);
            problem.Controls.Add(detail);
            problem.Controls.Add(retry);
            notesResultsHost.Controls.Add(problem);
            notesResultsHost.Height = 220;
            return;
        }
        int top = 0;
        if (loaded.RecoveredFromBackup)
        {
            int paneWidth = Math.Max(280, notesResultsHost.ClientSize.Width);
            RoundPanel recovery = NewCard(new Point(0, 0), new Size(paneWidth - 12, 64));
            recovery.BorderColor = amber;
            Label label = NewLabel("主便签文件无法读取，已从备份恢复；请核对后再保存。", 9.2f, FontStyle.Bold, amber);
            label.Location = new Point(22, 19);
            label.Size = new Size(Math.Max(120, paneWidth - 44), 26);
            recovery.Controls.Add(label);
            notesResultsHost.Controls.Add(recovery);
            top = 74;
        }
        List<NoteItem> notes = notesStore.Search(loaded.Document, notesSearchText, notesShowDeleted)
            .Where(note => note != null && note.IsDeleted == notesShowDeleted)
            .OrderByDescending(note => note.UpdatedAtUtc).ToList();
        notesSelectedIds.RemoveWhere(id => !notes.Any(note => string.Equals(note.Id, id, StringComparison.OrdinalIgnoreCase)));
        UpdateNotesSummaryButton();
        if (notes.Count == 0)
        {
            Button emptyAction;
            RoundPanel empty = CreateEmptyStateCard(notesShowDeleted ? "回收站" : "本机便签",
                notesShowDeleted ? "回收站是空的" : "先记下一件事",
                notesShowDeleted ? "删除的便签会暂时保留在这里，可以恢复；不会自动清空。" :
                    (string.IsNullOrWhiteSpace(notesSearchText) ? "语音、键盘和粘贴都可以先保存为普通文本。没有模型或遥控器时，便签仍然可用。" : "没有匹配的便签；可以清空搜索后继续。"),
                notesShowDeleted ? "返回收件箱" : "新建便签", out emptyAction);
            empty.Location = new Point(0, top);
            empty.Size = new Size(Math.Max(268, notesResultsHost.ClientSize.Width - 12), 286);
            empty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            emptyAction.Click += delegate
            {
                if (GuardNotesOperation("打开便签")) return;
                if (notesShowDeleted) { notesShowDeleted = false; ShowPage((int)VibePageId.Notes); }
                else CreateNoteAndEdit();
            };
            notesResultsHost.Controls.Add(empty);
            notesResultsHost.Height = top + 286;
            RenderNotesPreview(loaded);
            return;
        }

        int listWidth = Math.Max(280, notesResultsHost.ClientSize.Width);
        if (string.IsNullOrWhiteSpace(activeNoteId) || !notes.Any(note => note.Id == activeNoteId))
            activeNoteId = notes[0].Id;
        TableLayoutPanel list = new TableLayoutPanel();
        list.Name = "notesList";
        list.Location = new Point(0, top);
        list.Size = new Size(Math.Max(268, listWidth - 12), Math.Max(96, notes.Count * 92));
        list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        list.ColumnCount = 1;
        list.RowCount = notes.Count;
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        list.BackColor = Color.Transparent;
        int row = 0;
        foreach (NoteItem note in notes)
        {
            list.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
            list.Controls.Add(BuildNoteCard(note, row), 0, row++);
        }
        notesResultsHost.Controls.Add(list);
        notesResultsHost.Height = top + Math.Max(96, notes.Count * 92);
        RenderNotesPreview(loaded);
    }

    private Control BuildNoteCard(NoteItem note, int tabIndex)
    {
        int width = Math.Max(280, notesResultsHost == null ? 360 : notesResultsHost.ClientSize.Width - 12);
        RoundPanel card = NewCard(Point.Empty, new Size(width, 84));
        card.Dock = DockStyle.Fill;
        card.AccessibleName = note.DisplayTitle();
        card.AccessibleRole = AccessibleRole.ListItem;
        card.TabStop = true;
        card.TabIndex = tabIndex;
        CheckBox noteSelector = new CheckBox();
        noteSelector.Text = "";
        noteSelector.Name = "selectNoteForSummary";
        noteSelector.AccessibleName = "选择“" + note.DisplayTitle() + "”用于汇总";
        noteSelector.Checked = notesSelectedIds.Contains(note.Id);
        noteSelector.Location = new Point(14, 16);
        noteSelector.Size = new Size(24, 24);
        noteSelector.CheckedChanged += delegate
        {
            if (noteSelector.Checked) notesSelectedIds.Add(note.Id);
            else notesSelectedIds.Remove(note.Id);
            UpdateNotesSummaryButton();
        };
        Label title = NewLabel(note.DisplayTitle(), 11f, FontStyle.Bold, ink);
        title.Location = new Point(48, 14);
        title.Size = new Size(Math.Max(120, width - 204), 26);
        Label preview = NewLabel((note.Body ?? "").Replace("\r", "").Replace("\n", " "), 8.8f, FontStyle.Regular, muted);
        preview.Location = new Point(22, 42);
        preview.Size = new Size(Math.Max(120, width - 44), 24);
        preview.AutoEllipsis = true;
        Label meta = NewLabel((note.Category ?? "收件箱") + " · " + note.UpdatedAtUtc.ToLocalTime().ToString("MM-dd HH:mm"),
            8.2f, FontStyle.Regular, muted);
        meta.Location = new Point(Math.Max(22, width - 190), 14);
        meta.Size = new Size(118, 24);
        meta.TextAlign = ContentAlignment.MiddleRight;
        Button edit = SecondaryButton(note.IsDeleted ? "恢复" : "打开", new Point(Math.Max(22, width - 132), 19), new Size(66, 36));
        edit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        edit.Click += delegate
        {
            if (GuardNotesOperation(note.IsDeleted ? "恢复便签" : "打开便签")) return;
            if (note.IsDeleted) RestoreNote(note.Id);
            else ShowNoteEditor(note.Id, false);
        };
        Button remove = SecondaryButton(note.IsDeleted ? "清除" : "删除", new Point(Math.Max(94, width - 60), 19), new Size(58, 36));
        remove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        remove.Click += delegate
        {
            if (GuardNotesOperation(note.IsDeleted ? "清除便签" : "删除便签")) return;
            DeleteOrPurgeNote(note);
        };
        card.Controls.Add(noteSelector);
        card.Controls.Add(title);
        card.Controls.Add(preview);
        card.Controls.Add(meta);
        card.Controls.Add(edit);
        card.Controls.Add(remove);
        EventHandler noteSelectHandler = delegate { if (!note.IsDeleted) { activeNoteId = note.Id; RenderNotesPreview(notesStore.Load()); } };
        card.Click += noteSelectHandler;
        card.KeyDown += delegate(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Enter && args.KeyCode != Keys.Space) return;
            noteSelectHandler(card, EventArgs.Empty);
            args.Handled = true;
            args.SuppressKeyPress = true;
        };
        title.Click += noteSelectHandler;
        preview.Click += noteSelectHandler;
        meta.Click += noteSelectHandler;
        return card;
    }

    private void UpdateNotesSummaryButton()
    {
        if (notesSummarizeButton == null || notesSummarizeButton.IsDisposed) return;
        notesSummarizeButton.Enabled = !notesShowDeleted && notesSelectedIds.Count >= NotesBatchSummaryPolicy.MinimumSourceCount;
        notesSummarizeButton.Text = "汇总已选";
        notesSummarizeButton.AccessibleName = notesSummarizeButton.Enabled
            ? "汇总已选便签（已选 " + notesSelectedIds.Count + " 条）"
            : "汇总已选便签（至少选择两条）";
    }

    private void OpenBatchSummary()
    {
        if (GuardNotesOperation("汇总便签")) return;
        NotesLoadResult loaded = notesStore.Load();
        if (!loaded.IsSuccess || loaded.Document == null)
        {
            ShowActionToast(ActionResult.Create("汇总便签", "本机便签", ActionState.Error,
                "便签文件无法读取，未发送内容", "本地文件损坏或不可访问", "重新检测便签后重试", loaded.ErrorCode));
            return;
        }
        List<NoteItem> selected = loaded.Document.notes
            .Where(note => note != null && !note.IsDeleted && notesSelectedIds.Contains(note.Id))
            .Select(note => note.Copy()).ToList();
        if (selected.Count < NotesBatchSummaryPolicy.MinimumSourceCount)
        {
            ShowActionToast(ActionResult.Create("汇总便签", "已选便签", ActionState.Warning,
                "至少选择两条便签", "本次没有发送内容", "勾选两条或更多便签后重试", "NOTES-SUMMARY-NEEDS-MULTIPLE"));
            return;
        }
        using (NotesBatchSummaryForm dialog = new NotesBatchSummaryForm(notesStore, aiProviderStore,
            aiTextService, selected, this))
        {
            dialog.ShowDialog(this);
        }
        ShowPage((int)VibePageId.Notes);
    }

    private void ClearNotesPreview()
    {
        if (notesPreviewHost == null || notesPreviewHost.IsDisposed) return;
        while (notesPreviewHost.Controls.Count > 0)
        {
            Control old = notesPreviewHost.Controls[0];
            notesPreviewHost.Controls.RemoveAt(0);
            DisposeOwnedControlResources(old);
            old.Dispose();
        }
    }

    private void RenderNotesPreview(NotesLoadResult loaded)
    {
        if (notesPreviewHost == null || notesPreviewHost.IsDisposed) return;
        ClearNotesPreview();
        int width = Math.Max(320, notesPreviewHost.ClientSize.Width - 24);
        if (loaded == null || !loaded.IsSuccess)
        {
            Label unavailable = NewLabel("正文预览暂不可用\r\n请先重新检测本机便签。", 11f, FontStyle.Bold, muted);
            unavailable.Location = new Point(20, 22);
            unavailable.Size = new Size(width, 70);
            notesPreviewHost.Controls.Add(unavailable);
            return;
        }
        NoteItem note = notesStore.Search(loaded.Document, notesSearchText, notesShowDeleted)
            .Where(item => item != null && item.IsDeleted == notesShowDeleted)
            .FirstOrDefault(item => item.Id == activeNoteId);
        if (note == null)
        {
            Label empty = NewLabel("选择左侧便签查看正文", 11f, FontStyle.Bold, muted);
            empty.Location = new Point(20, 22);
            empty.Size = new Size(width, 40);
            notesPreviewHost.Controls.Add(empty);
            return;
        }
        RoundPanel previewCard = NewCard(new Point(12, 12), new Size(width, Math.Max(320, notesPreviewHost.ClientSize.Height - 24)));
        previewCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Label title = NewLabel(note.DisplayTitle(), 15f, FontStyle.Bold, ink);
        title.Location = new Point(22, 20);
        title.Size = new Size(Math.Max(160, width - 44), 32);
        Label meta = NewLabel((note.Category ?? "收件箱") + " · 修订 " + note.Revision + " · " + note.UpdatedAtUtc.ToLocalTime().ToString("MM-dd HH:mm"),
            8.5f, FontStyle.Regular, muted);
        meta.Location = new Point(22, 56);
        meta.Size = new Size(Math.Max(160, width - 44), 24);
        TextBox body = new TextBox();
        body.Multiline = true;
        body.ReadOnly = true;
        body.ScrollBars = ScrollBars.Vertical;
        body.BackColor = surfaceBackground;
        body.ForeColor = ink;
        body.BorderStyle = BorderStyle.FixedSingle;
        body.Location = new Point(22, 88);
        body.Size = new Size(Math.Max(160, width - 44), Math.Max(140, previewCard.Height - 154));
        body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        body.Font = new Font("Microsoft YaHei UI", 10.5f);
        body.Text = note.Body ?? "";
        Button edit = SecondaryButton("在窗口中编辑", new Point(22, Math.Max(112, previewCard.Height - 52)), new Size(120, 36));
        edit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        edit.Click += delegate { if (!GuardNotesOperation("编辑便签")) ShowNoteEditor(note.Id, false); };
        previewCard.Controls.Add(title);
        previewCard.Controls.Add(meta);
        previewCard.Controls.Add(body);
        previewCard.Controls.Add(edit);
        notesPreviewHost.Controls.Add(previewCard);
    }

    private void CreateNoteAndEdit()
    {
        if (GuardNotesOperation("新建便签")) return;
        NotesLoadResult loaded = notesStore.Load();
        if (!loaded.IsSuccess)
        {
            AddNotesProblem(loaded.ErrorCode);
            return;
        }
        NoteItem note = new NoteItem();
        loaded.Document.notes.Add(note);
        string error;
        if (!notesStore.TrySave(loaded.Document, out error))
        {
            ShowActionToast(ActionResult.Create("新建便签", "本机便签", ActionState.Error,
                "便签没有保存", "磁盘写入失败，编辑内容尚未创建", "重新检测磁盘权限后重试", error));
            return;
        }
        activeNoteId = note.Id;
        ShowNoteEditor(note.Id, true);
    }

    private void ShowNoteEditor(string noteId, bool newNote)
    {
        if (GuardNotesOperation("打开便签")) return;
        NotesLoadResult loaded = notesStore.Load();
        NoteItem note = loaded.IsSuccess && loaded.Document.notes != null
            ? loaded.Document.notes.FirstOrDefault(item => item != null && item.Id == noteId && !item.IsDeleted) : null;
        if (note == null) return;
        Form editor = new Form();
        editor.Text = "编辑便签 · " + note.DisplayTitle();
        editor.StartPosition = FormStartPosition.CenterParent;
        editor.Size = new Size(720, 560);
        editor.MinimumSize = new Size(560, 520);
        editor.AutoScaleMode = AutoScaleMode.Dpi;
        Label titleLabel = NewLabel("标题（可留空）", 9.2f, FontStyle.Bold, ink);
        titleLabel.Location = new Point(24, 20);
        titleLabel.Size = new Size(160, 26);
        TextBox title = new TextBox();
        title.Location = new Point(24, 48);
        title.Size = NotesEditorLayoutPolicy.GetFieldSize(editor.ClientSize.Width, 48, 34);
        title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        title.Text = note.Title ?? "";
        title.Font = new Font("Microsoft YaHei UI", 10f);
        Label categoryLabel = NewLabel("分类（可输入新分类）", 9.2f, FontStyle.Bold, ink);
        categoryLabel.Location = new Point(24, 94);
        categoryLabel.Size = new Size(220, 26);
        TextBox category = new TextBox();
        category.Name = "noteCategoryBox";
        category.Location = new Point(24, 122);
        category.Size = NotesEditorLayoutPolicy.GetFieldSize(editor.ClientSize.Width, 48, 34);
        category.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        string initialCategory;
        string initialCategoryError;
        category.Text = NoteCategoryPolicy.TryNormalize(note.Category, out initialCategory, out initialCategoryError)
            ? initialCategory : NoteCategoryPolicy.DefaultCategory;
        category.Font = new Font("Microsoft YaHei UI", 10f);
        category.AccessibleName = "便签分类，可输入新的平级分类";
        Label bodyLabel = NewLabel("正文", 9.2f, FontStyle.Bold, ink);
        bodyLabel.Location = new Point(24, 168);
        bodyLabel.Size = new Size(160, 26);
        TextBox body = new TextBox();
        body.Multiline = true;
        body.ScrollBars = ScrollBars.Vertical;
        body.AcceptsReturn = true;
        body.Location = new Point(24, 196);
        body.Size = new Size(
            NotesEditorLayoutPolicy.GetFieldSize(editor.ClientSize.Width, 48, 34).Width,
            Math.Max(120, editor.ClientSize.Height - 286));
        body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        body.Text = note.Body ?? "";
        body.Font = new Font("Microsoft YaHei UI", 11f);
        long editorRevision = note.Revision;
        bool isNewNote = newNote;
        bool editorDirty = false;
        bool suppressEditorEvents = true;
        bool closeWithoutFlush = false;
        bool draftDiscarded = false;
        Timer autoSaveTimer = new Timer { Interval = 500 };
        Label saveStatus = NewLabel("已保存 · 修订 " + editorRevision, 8.5f, FontStyle.Regular, muted);
        saveStatus.Location = new Point(360, 464);
        saveStatus.Size = new Size(300, 28);
        saveStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Func<bool, bool> saveEditor = null;
        saveEditor = delegate(bool explicitSave)
        {
            if (!editorDirty && !explicitSave) return true;
            if (GuardNotesOperation("保存便签")) return false;
            string error;
            string nextTitle = title.Text ?? "";
            string nextBody = body.Text ?? "";
            string nextCategory;
            string categoryError;
            if (!NoteCategoryPolicy.TryNormalize(category.Text, out nextCategory, out categoryError))
            {
                saveStatus.Text = "分类未保存 · " + categoryError;
                saveStatus.ForeColor = coral;
                return false;
            }
            if (!notesStore.TryUpdate(note.Id, editorRevision, delegate(NoteItem latest)
            {
                latest.Title = nextTitle;
                latest.Body = nextBody;
                latest.Category = nextCategory;
                return true;
            }, out error))
            {
                autoSaveTimer.Stop();
                saveStatus.Text = error == "NOTES-REVISION-CONFLICT"
                    ? "其他窗口已修改 · 本次未覆盖，请重新打开"
                    : "保存失败 · " + error;
                saveStatus.ForeColor = coral;
                return false;
            }
            editorRevision++;
            editorDirty = false;
            if (explicitSave) isNewNote = false;
            autoSaveTimer.Stop();
            suppressEditorEvents = true;
            try { category.Text = nextCategory; }
            finally { suppressEditorEvents = false; }
            saveStatus.Text = (explicitSave ? "已保存" : "已自动保存") + " · 修订 " + editorRevision;
            saveStatus.ForeColor = green;
            return true;
        };
        Action scheduleAutoSave = delegate
        {
            if (suppressEditorEvents || closeWithoutFlush) return;
            editorDirty = true;
            saveStatus.Text = "有未保存修改 · 约 500ms 后自动保存";
            saveStatus.ForeColor = cyan;
            autoSaveTimer.Stop();
            if (NoteDraftPolicy.ShouldScheduleAutoSave(editorDirty)) autoSaveTimer.Start();
        };
        title.TextChanged += delegate { scheduleAutoSave(); };
        category.TextChanged += delegate { scheduleAutoSave(); };
        body.TextChanged += delegate { scheduleAutoSave(); };
        autoSaveTimer.Tick += delegate
        {
            autoSaveTimer.Stop();
            if (!editorDirty) return;
            if (NoteDraftPolicy.ShouldDeferAutoSave(editorDirty, IsNotesRecordingActive()))
            {
                saveStatus.Text = "录音进行中 · 结束后自动保存";
                saveStatus.ForeColor = cyan;
                autoSaveTimer.Start();
                return;
            }
            saveEditor(false);
        };
        suppressEditorEvents = false;
        Button save = PrimaryButton("保存", new Point(24, 456), new Size(96, 40));
        save.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Button cancel = SecondaryButton("取消", new Point(132, 456), new Size(96, 40));
        cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Button ai = SecondaryButton("AI 整理", new Point(240, 456), new Size(112, 40));
        ai.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        ai.Click += delegate
        {
            if (GuardNotesOperation("打开 AI 整理")) return;
            if (!saveEditor(false)) return;
            NoteItem current = new NoteItem
            {
                Id = note.Id, Title = title.Text ?? "", Body = body.Text ?? "", Category = category.Text ?? note.Category,
                CreatedAtUtc = note.CreatedAtUtc, UpdatedAtUtc = note.UpdatedAtUtc, Revision = editorRevision,
                IsDeleted = false, AiResults = note.AiResults == null ? new List<NoteAiResult>() : note.AiResults
            };
            using (NotesAiOperationForm aiDialog = new NotesAiOperationForm(notesStore, aiProviderStore, aiTextService, current, editor))
                aiDialog.ShowDialog(editor);
        };
        save.Click += delegate
        {
            if (saveEditor(true))
            {
                editor.Close();
                ShowPage((int)VibePageId.Notes);
            }
        };
        cancel.Click += delegate
        {
            if (isNewNote)
            {
                string discardError;
                if (!notesStore.TryDiscardDraft(note.Id, editorRevision, out discardError))
                {
                    ShowActionToast(ActionResult.Create("取消新建便签", "本机便签", ActionState.Error,
                        "新便签未能取消", "便签已被其他窗口修改或本地存储不可用", "重新打开后检查并重试", discardError));
                    return;
                }
                draftDiscarded = true;
                isNewNote = false;
            }
            closeWithoutFlush = true;
            editorDirty = false;
            autoSaveTimer.Stop();
            editor.Close();
        };
        editor.Controls.Add(titleLabel);
        editor.Controls.Add(title);
        editor.Controls.Add(categoryLabel);
        editor.Controls.Add(category);
        editor.Controls.Add(bodyLabel);
        editor.Controls.Add(body);
        editor.Controls.Add(save);
        editor.Controls.Add(cancel);
        editor.Controls.Add(ai);
        editor.Controls.Add(saveStatus);
        editor.AcceptButton = save;
        editor.CancelButton = cancel;
        editor.FormClosing += delegate(object sender, FormClosingEventArgs args)
        {
            if (closeWithoutFlush) return;
            if (NoteDraftPolicy.ShouldDiscardNewDraft(isNewNote, title.Text, body.Text))
            {
                string discardError;
                if (!notesStore.TryDiscardDraft(note.Id, editorRevision, out discardError))
                {
                    ShowActionToast(ActionResult.Create("关闭新建便签", "本机便签", ActionState.Error,
                        "新便签未能取消", "便签已被其他窗口修改或本地存储不可用", "重新打开后检查并重试", discardError));
                    args.Cancel = true;
                    return;
                }
                draftDiscarded = true;
                isNewNote = false;
                closeWithoutFlush = true;
                editorDirty = false;
                autoSaveTimer.Stop();
                return;
            }
            if (editorDirty && !saveEditor(false)) args.Cancel = true;
            else if (editorDirty) isNewNote = false;
        };
        editor.FormClosed += delegate
        {
            autoSaveTimer.Stop();
            autoSaveTimer.Dispose();
            if (draftDiscarded && !IsDisposed) ShowPage((int)VibePageId.Notes);
        };
        editor.Shown += delegate { if (!IsNotesRecordingActive()) body.Focus(); else SetEditorRecordingNotice(editor); };
        // Building the editor yields to layout and message processing. Check
        // again at the actual modal boundary so recording still has priority.
        if (GuardNotesOperation("打开便签编辑器"))
        {
            editor.Close();
            return;
        }
        editor.ShowDialog(this);
    }

    private void SetEditorRecordingNotice(Form editor)
    {
        if (editor == null || editor.IsDisposed) return;
        editor.Text = "录音正在进行 · 便签编辑暂不抢焦点";
    }

    private void RestoreNote(string noteId)
    {
        if (GuardNotesOperation("恢复便签")) return;
        NotesLoadResult loaded = notesStore.Load();
        string error = "NOTES-STORE-READ-FAILED";
        if (!loaded.IsSuccess || !notesStore.TryRestore(loaded.Document, noteId, out error))
        {
            ShowActionToast(ActionResult.Create("恢复便签", "回收站", ActionState.Error,
                "便签没有恢复", "记录不存在或本地文件不可写", "重新检测后重试", error));
            return;
        }
        ShowPage((int)VibePageId.Notes);
    }

    private void DeleteOrPurgeNote(NoteItem note)
    {
        if (note == null) return;
        if (GuardNotesOperation(note.IsDeleted ? "清除便签" : "删除便签")) return;
        if (note.IsDeleted)
        {
            DialogResult confirmed = MessageBox.Show(this,
                "清除后将无法从回收站恢复这条便签。\r\n是否继续？", "确认清除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmed != DialogResult.Yes) return;
            NotesLoadResult loaded = notesStore.Load();
            if (!loaded.IsSuccess) return;
            loaded.Document.notes.RemoveAll(item => item != null && item.Id == note.Id);
            string error;
            if (!notesStore.TrySave(loaded.Document, out error)) ShowActionToast(ActionResult.Create(
                "清除便签", "回收站", ActionState.Error, "便签没有清除", "本地文件不可写", "保留记录并重试", error));
            else ShowPage((int)VibePageId.Notes);
            return;
        }
        NotesLoadResult document = notesStore.Load();
        string deleteError = "NOTES-STORE-READ-FAILED";
        if (!document.IsSuccess || !notesStore.TryDelete(document.Document, note.Id, out deleteError))
        {
            ShowActionToast(ActionResult.Create("删除便签", note.DisplayTitle(), ActionState.Error,
                "便签没有删除", "本次没有改变原文", "重新检测后重试", deleteError));
            return;
        }
        ShowPage((int)VibePageId.Notes);
    }

    private void ExportNotes(bool markdown)
    {
        NotesLoadResult loaded = notesStore.Load();
        if (!loaded.IsSuccess) { AddNotesProblem(loaded.ErrorCode); return; }
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Filter = markdown ? "Markdown (*.md)|*.md" : "Text (*.txt)|*.txt";
            dialog.DefaultExt = markdown ? "md" : "txt";
            dialog.AddExtension = true;
            dialog.FileName = markdown ? "vibe-flow-notes.md" : "vibe-flow-notes.txt";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string error;
            bool exported = notesStore.Export(loaded.Document.notes, dialog.FileName, markdown, out error);
            if (!exported)
                ShowActionToast(ActionResult.Create("导出便签", "本机文件", ActionState.Error,
                    "便签没有导出", "目标文件不可写", "选择其他目录后重试", error));
            else ShowActionToast(ActionResult.Create("导出便签", "本机文件", ActionState.Success,
                markdown ? "Markdown 文件已写入" : "TXT 文件已写入",
                "只导出了未删除的本地文本", "打开目标文件检查内容", ""));
        }
    }

    private void BackupNotesJson()
    {
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Filter = "便签 JSON 备份 (*.json)|*.json";
            dialog.FileName = "vibe-flow-notes-backup.json";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string error;
            if (!notesStore.TryExportJsonBackup(dialog.FileName, out error))
            {
                ShowActionToast(ActionResult.Create("备份便签", "本机 JSON 文件", ActionState.Error,
                    "便签备份没有写入", "目标文件不可写或路径不安全", "选择其他目录后重试", error));
                return;
            }
            ShowActionToast(ActionResult.Create("备份便签", "本机 JSON 文件", ActionState.Success,
                "便签 JSON 备份已写入", "没有包含语音配置或 API Key", "保留该文件以便以后恢复", ""));
        }
    }

    private void RestoreNotesJson()
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Filter = "便签 JSON 备份 (*.json)|*.json";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            DialogResult confirmed = MessageBox.Show(this,
                "恢复会替换当前本机便签；当前版本会留在 notes.json.bak。\r\n不会改变录音、遥控器或语音工具配置。\r\n是否继续？",
                "确认恢复便签", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmed != DialogResult.Yes) return;
            string error;
            if (!notesStore.TryRestoreJsonBackup(dialog.FileName, out error))
            {
                ShowActionToast(ActionResult.Create("恢复便签", "本机 JSON 文件", ActionState.Error,
                    "便签没有恢复", "备份格式、路径或本地文件不可用；当前便签没有改变", "选择有效备份后重试", error));
                return;
            }
            ShowActionToast(ActionResult.Create("恢复便签", "本机 JSON 文件", ActionState.Success,
                "便签已恢复", "当前便签来自用户选择的本机备份", "重新打开需要编辑的便签", ""));
            ShowPage((int)VibePageId.Notes);
        }
    }

    private void AddNotesProblem(string errorCode)
    {
        if (notesResultsHost != null && !notesResultsHost.IsDisposed)
        {
            RenderNotesResults(NotesLoadResult.Failure(errorCode));
            return;
        }
        RoundPanel problem = NewCard(new Point(34, 146), new Size(960, 220));
        problem.BorderColor = coral;
        Label title = NewLabel("便签暂时不可用", 12f, FontStyle.Bold, coral);
        title.Location = new Point(24, 22);
        title.Size = new Size(900, 32);
        Label detail = NewLabel("发生了什么：本地便签文件无法读取。\r\n可能原因：文件损坏或用户数据目录不可访问。\r\n本次影响：没有执行编辑、删除或导出。\r\n错误码：" + errorCode,
            9.2f, FontStyle.Regular, muted);
        detail.Location = new Point(24, 64);
        detail.Size = new Size(900, 90);
        Button retry = PrimaryButton("重新检测", new Point(24, 166), new Size(112, 40));
        retry.Click += delegate { ShowPage((int)VibePageId.Notes); };
        problem.Controls.Add(title);
        problem.Controls.Add(detail);
        problem.Controls.Add(retry);
        content.Controls.Add(problem);
    }
}

internal static class NotesEditorLayoutPolicy
{
    internal static Size GetFieldSize(int clientWidth, int horizontalMargin, int height)
    {
        return new Size(Math.Max(1, clientWidth - horizontalMargin), Math.Max(1, height));
    }
}

internal static class NotesPageLayoutPolicy
{
    internal static void GetPaneWidths(int totalWidth, out int listWidth, out int previewWidth)
    {
        int safeWidth = Math.Max(640, totalWidth);
        listWidth = Math.Max(280, Math.Min(420, (int)(safeWidth * 0.4f)));
        previewWidth = safeWidth - listWidth;
        if (previewWidth < 360)
        {
            previewWidth = 360;
            listWidth = safeWidth - previewWidth;
        }
    }
}
