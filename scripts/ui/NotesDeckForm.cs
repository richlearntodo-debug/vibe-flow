using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Windows.Forms;

internal sealed class NotesDeckForm : Form
{
    private readonly NotesStore store;
    private readonly AiProviderStore aiStore;
    private readonly AiTextService aiService;
    private readonly Func<bool> isRecording;
    private readonly Form owner;
    private readonly Action refreshMain;
    private readonly string preferencesPath;
    private readonly ListBox list = new ListBox();
    private readonly TextBox title = new TextBox();
    private readonly TextBox category = new TextBox();
    private readonly TextBox body = new TextBox();
    private readonly Label status = new Label();
    private readonly Timer autoSaveTimer = new Timer();
    private readonly ToolTip deckToolTip = new ToolTip();
    private bool fixedPanel;
    private bool hasFixedBounds;
    private bool restoringFixedBounds;
    private Rectangle fixedBounds;
    private bool closing;
    private bool suppressEditorEvents;
    private bool dirty;
    private bool activeIsNew;
    private string activeId = "";
    private long activeRevision;
    private bool hasSavedLocation;
    private Point savedLocation;

    internal NotesDeckForm(NotesStore notesStore, AiProviderStore providerStore, AiTextService textService,
        Func<bool> recording, Form ownerForm, Action refresh)
    {
        store = notesStore;
        aiStore = providerStore;
        aiService = textService;
        isRecording = recording ?? delegate { return false; };
        owner = ownerForm;
        refreshMain = refresh;
        preferencesPath = Path.Combine(Path.GetDirectoryName(store.PathOnDisk), "notes-deck-preferences.json");
        Text = "便签 Deck · Vibe Flow";
        Width = 380;
        Height = 720;
        MinimumSize = new Size(320, 480);
        MaximumSize = new Size(480, 1200);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(247, 248, 250);
        Font = new Font("Microsoft YaHei UI", 9f);
        ShowInTaskbar = true;
        LoadPreferences();
        SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
        LocationChanged += delegate
        {
            EnforceFixedBounds();
            if (!closing) SavePreferences();
        };
        SizeChanged += delegate
        {
            EnforceFixedBounds();
            if (!closing) SavePreferences();
        };
        autoSaveTimer.Interval = 500;
        autoSaveTimer.Tick += delegate
        {
            autoSaveTimer.Stop();
            if (!dirty) return;
            if (NoteDraftPolicy.ShouldDeferAutoSave(dirty, IsRecordingGuard()))
            {
                SetStatus("录音进行中 · 结束后自动保存", Color.FromArgb(0, 127, 142));
                autoSaveTimer.Start();
                return;
            }
            TrySaveActive(false);
        };
        BuildControls();
        LoadNotes("");
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x02E0) ReclampAfterDisplayChange(); // WM_DPICHANGED
    }

    internal bool ShowDeck()
    {
        if (IsDisposed || IsRecordingGuard()) return false;
        if (!FlushPendingSave()) return false;
        // The save can yield to the UI message pump. Re-check immediately
        // before showing so a newly started recording always wins focus.
        if (IsDisposed || IsRecordingGuard()) return false;
        if (!Visible)
        {
            // Re-clamp a saved fixed window when the work area changes before
            // capturing the new position/size lock.
            hasFixedBounds = false;
            PositionToRightOfOwner();
            CaptureFixedBounds();
        }
        // Keep the Deck as an independent top-level tool window. This avoids
        // binding its lifetime or accessibility surface to the main page.
        Show();
        LoadNotes(activeId);
        return true;
    }

    internal void ApplyTheme(bool useDarkTheme)
    {
        if (IsDisposed) return;
        Color background = useDarkTheme ? Color.FromArgb(25, 26, 31) : Color.FromArgb(247, 248, 250);
        Color surface = useDarkTheme ? Color.FromArgb(35, 37, 44) : Color.White;
        Color ink = useDarkTheme ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
        Color muted = useDarkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(96, 106, 120);
        BackColor = background;
        ApplyThemeToControl(this, background, surface, ink, muted, useDarkTheme);
        Invalidate(true);
    }

    private static void ApplyThemeToControl(Control control, Color background, Color surface,
        Color ink, Color muted, bool useDarkTheme)
    {
        if (control == null) return;
        Button button = control as Button;
        TextBox textBox = control as TextBox;
        ListBox listBox = control as ListBox;
        Label label = control as Label;
        if (button != null)
        {
            button.BackColor = useDarkTheme ? Color.FromArgb(48, 50, 59) : Color.White;
            button.ForeColor = ink;
        }
        else if (textBox != null)
        {
            textBox.BackColor = useDarkTheme ? Color.FromArgb(31, 33, 39) : Color.White;
            textBox.ForeColor = ink;
        }
        else if (listBox != null)
        {
            listBox.BackColor = surface;
            listBox.ForeColor = ink;
        }
        else if (label != null)
        {
            label.ForeColor = (label.Font.Style & FontStyle.Bold) == FontStyle.Bold ? ink : muted;
            label.BackColor = Color.Transparent;
        }
        else if (control is FlowLayoutPanel)
        {
            control.BackColor = surface;
        }
        else if (control is Panel)
        {
            control.BackColor = background;
        }
        foreach (Control child in control.Controls)
            ApplyThemeToControl(child, background, surface, ink, muted, useDarkTheme);
    }

    internal void HideForRecording()
    {
        if (IsDisposed) return;
        try
        {
            if (Visible) Hide();
            SetStatus("录音正在进行；便签 Deck 已隐藏", Color.FromArgb(204, 70, 82));
        }
        catch { }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Losing focus must not close the Deck. Fixed visibility and TopMost
        // control persistence and z-order separately; only explicit close or
        // recording-priority handling may hide it.
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!closing && e.CloseReason == CloseReason.UserClosing)
        {
            if (!DiscardBlankNewDraft() || !FlushPendingSave())
            {
                e.Cancel = true;
                return;
            }
            e.Cancel = true;
            Hide();
            return;
        }
        if (closing && (!DiscardBlankNewDraft() || !FlushPendingSave()))
        {
            e.Cancel = true;
            closing = false;
            return;
        }
        base.OnFormClosing(e);
    }

    internal bool CloseWithOwner()
    {
        if (!DiscardBlankNewDraft() || !FlushPendingSave()) return false;
        closing = true;
        try { Close(); }
        catch
        {
            closing = false;
            return false;
        }
        return IsDisposed;
    }

    private void BuildControls()
    {
        FlowLayoutPanel toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(10, 8, 8, 4),
            WrapContents = false,
            BackColor = Color.White
        };
        Button pin = ButtonFor("固定", 52);
        pin.Name = "notesDeckPinButton";
        pin.Click += delegate
        {
            fixedPanel = !fixedPanel;
            if (fixedPanel) CaptureFixedBounds();
            else hasFixedBounds = false;
            pin.Text = fixedPanel ? "已固定" : "固定";
            SavePreferences();
        };
        Button top = ButtonFor("置顶", 52);
        top.Name = "notesDeckTopMostButton";
        top.Click += delegate
        {
            TopMost = !TopMost;
            top.Text = TopMost ? "已置顶" : "置顶";
            SavePreferences();
        };
        Button create = ButtonFor("新建", 52);
        create.Click += delegate { CreateNote(); };
        Button close = ButtonFor("关闭", 52);
        close.Click += delegate
        {
            if (DiscardBlankNewDraft() && FlushPendingSave()) Hide();
        };
        toolbar.Controls.Add(pin);
        toolbar.Controls.Add(top);
        toolbar.Controls.Add(create);
        toolbar.Controls.Add(close);
        Label heading = new Label { Text = "本机便签", Dock = DockStyle.Top, Height = 32, Padding = new Padding(12, 8, 8, 0), Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(25, 33, 43) };
        list.Dock = DockStyle.Top;
        list.Height = 142;
        list.IntegralHeight = false;
        list.AccessibleName = "便签列表";
        list.SelectedIndexChanged += delegate
        {
            NoteItem selected = list.SelectedItem as NoteItem;
            if (selected != null) LoadNote(selected);
        };
        // Add top-docked controls from bottom to top so WinForms lays out the
        // toolbar first, followed by the heading and note list.
        Controls.Add(list);
        Controls.Add(heading);
        Controls.Add(toolbar);

        Panel editor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 8), BackColor = Color.FromArgb(247, 248, 250) };
        Label titleLabel = new Label { Text = "标题（可留空）", Dock = DockStyle.Top, Height = 22, ForeColor = Color.FromArgb(96, 106, 120) };
        editor.Controls.Add(titleLabel);
        title.Dock = DockStyle.Top;
        title.Height = 30;
        title.Font = new Font(Font, FontStyle.Regular);
        title.AccessibleName = "便签标题";
        title.TextChanged += delegate { MarkDirty(); };
        editor.Controls.Add(title);
        Label categoryLabel = new Label { Text = "分类（可输入新分类）", Dock = DockStyle.Top, Height = 22, ForeColor = Color.FromArgb(96, 106, 120) };
        editor.Controls.Add(categoryLabel);
        category.Dock = DockStyle.Top;
        category.Height = 30;
        category.Font = new Font(Font, FontStyle.Regular);
        category.AccessibleName = "便签分类，可输入新的平级分类";
        category.TextChanged += delegate { MarkDirty(); };
        editor.Controls.Add(category);
        Label bodyLabel = new Label { Text = "正文", Dock = DockStyle.Top, Height = 24, Padding = new Padding(0, 8, 0, 0), ForeColor = Color.FromArgb(96, 106, 120) };
        editor.Controls.Add(bodyLabel);
        body.Multiline = true;
        body.AcceptsReturn = true;
        body.ScrollBars = ScrollBars.Vertical;
        body.Dock = DockStyle.Fill;
        body.Font = new Font("Microsoft YaHei UI", 11f);
        body.AccessibleName = "便签正文";
        body.TextChanged += delegate { MarkDirty(); };
        editor.Controls.Add(body);
        FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(0, 8, 0, 0), WrapContents = true };
        Button save = ButtonFor("保存", 68);
        save.Click += delegate { SaveActive(); };
        Button ai = ButtonFor("AI 整理", 78);
        ai.Click += delegate { OpenAi(); };
        Button copy = ButtonFor("复制", 62);
        copy.Click += delegate
        {
            if (IsRecordingGuard()) return;
            Clipboard.SetText(body.Text ?? "");
            SetStatus("正文已复制到剪贴板", Color.FromArgb(10, 164, 104));
        };
        actions.Controls.Add(save);
        actions.Controls.Add(ai);
        actions.Controls.Add(copy);
        status.AutoEllipsis = true;
        status.Height = 28;
        status.Width = 330;
        status.ForeColor = Color.FromArgb(96, 106, 120);
        actions.Controls.Add(status);
        editor.Controls.Add(actions);
        Controls.Add(editor);
        top.Text = TopMost ? "已置顶" : "置顶";
        pin.Text = fixedPanel ? "已固定" : "固定";
        deckToolTip.SetToolTip(pin, "固定当前位置和大小；不改变置顶状态");
        deckToolTip.SetToolTip(top, "置顶显示；不锁定当前位置和大小");
    }

    private void CaptureFixedBounds()
    {
        if (!fixedPanel)
        {
            hasFixedBounds = false;
            return;
        }
        fixedBounds = Bounds;
        hasFixedBounds = true;
    }

    private void EnforceFixedBounds()
    {
        if (!NotesDeckWindowPolicy.ShouldRestoreFixedBounds(fixedPanel, hasFixedBounds,
            Bounds, fixedBounds) || restoringFixedBounds) return;
        restoringFixedBounds = true;
        try { Bounds = fixedBounds; }
        finally { restoringFixedBounds = false; }
    }

    private Button ButtonFor(string text, int width)
    {
        return new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(18, 30, 54), Margin = new Padding(2, 0, 2, 4) };
    }

    private bool IsRecordingGuard()
    {
        bool recording = false;
        try { recording = isRecording(); } catch { recording = true; }
        if (!recording) return false;
        SetStatus("录音正在进行；便签不会抢焦点，请结束后重试", Color.FromArgb(204, 70, 82));
        return true;
    }

    private void CreateNote()
    {
        if (IsRecordingGuard()) return;
        if (!DiscardBlankNewDraft() || !FlushPendingSave()) return;
        NotesLoadResult loaded = store.Load();
        if (!loaded.IsSuccess) { SetStatus("便签读取失败：" + loaded.ErrorCode, Color.FromArgb(204, 70, 82)); return; }
        NoteItem note = new NoteItem();
        loaded.Document.notes.Add(note);
        string error;
        if (!store.TrySave(loaded.Document, out error)) { SetStatus("新便签没有保存：" + error, Color.FromArgb(204, 70, 82)); return; }
        activeId = note.Id;
        activeRevision = note.Revision;
        activeIsNew = true;
        LoadNotes(activeId);
        // LoadNotes selects the item and refreshes the editor; restore the
        // transient-new marker after that refresh so Cancel/Close can discard it.
        activeIsNew = true;
        body.Focus();
    }

    private void LoadNotes(string preferredId)
    {
        NotesLoadResult loaded = store.Load();
        if (!loaded.IsSuccess) { SetStatus("便签读取失败：" + loaded.ErrorCode, Color.FromArgb(204, 70, 82)); return; }
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (NoteItem note in loaded.Document.notes.Where(item => item != null && !item.IsDeleted).OrderByDescending(item => item.UpdatedAtUtc))
                list.Items.Add(note);
            NoteItem selected = list.Items.Cast<NoteItem>().FirstOrDefault(item => item.Id == preferredId) ?? list.Items.Cast<NoteItem>().FirstOrDefault();
            if (selected != null) list.SelectedItem = selected;
            else
            {
                activeId = "";
                title.Text = "";
                category.Text = NoteCategoryPolicy.DefaultCategory;
                body.Text = "";
                activeRevision = 0;
                activeIsNew = false;
            }
        }
        finally { list.EndUpdate(); }
    }

    private void LoadNote(NoteItem note)
    {
        if (note == null) return;
        bool preserveNewDraft = activeIsNew && string.Equals(activeId, note.Id, StringComparison.OrdinalIgnoreCase);
        if (dirty && !string.Equals(activeId, note.Id, StringComparison.OrdinalIgnoreCase) && !TrySaveActive(false))
            return;
        activeId = note.Id;
        activeRevision = note.Revision;
        activeIsNew = preserveNewDraft;
        suppressEditorEvents = true;
        try
        {
            title.Text = note.Title ?? "";
            string normalizedCategory;
            string categoryError;
            category.Text = NoteCategoryPolicy.TryNormalize(note.Category, out normalizedCategory, out categoryError)
                ? normalizedCategory : NoteCategoryPolicy.DefaultCategory;
            body.Text = note.Body ?? "";
            dirty = false;
            autoSaveTimer.Stop();
        }
        finally { suppressEditorEvents = false; }
        SetStatus("已打开 · 修订 " + activeRevision, Color.FromArgb(96, 106, 120));
    }

    private void SaveActive()
    {
        TrySaveActive(true);
    }

    private bool TrySaveActive(bool explicitSave)
    {
        if (string.IsNullOrWhiteSpace(activeId)) return true;
        if (!dirty && !explicitSave) return true;
        if (IsRecordingGuard()) return false;
        string nextTitle = title.Text ?? "";
        string nextCategory;
        string categoryError;
        if (!NoteCategoryPolicy.TryNormalize(category.Text, out nextCategory, out categoryError))
        {
            SetStatus("分类未保存：" + categoryError, Color.FromArgb(204, 70, 82));
            autoSaveTimer.Stop();
            return false;
        }
        string nextBody = body.Text ?? "";
        string error;
        if (!store.TryUpdate(activeId, activeRevision, delegate(NoteItem note)
        {
            note.Title = nextTitle;
            note.Category = nextCategory;
            note.Body = nextBody;
            return true;
        }, out error))
        {
            SetStatus(error == "NOTES-REVISION-CONFLICT" ? "原文已在其他窗口修改；本次未覆盖，请重新打开后合并" : "保存失败：" + error,
                Color.FromArgb(204, 70, 82));
            autoSaveTimer.Stop();
            return false;
        }
        activeRevision++;
        if (explicitSave) activeIsNew = false;
        dirty = false;
        autoSaveTimer.Stop();
        SetStatus(explicitSave ? "已保存到本机 · 修订 " + activeRevision : "已自动保存到本机 · 修订 " + activeRevision,
            Color.FromArgb(10, 164, 104));
        try { if (refreshMain != null) refreshMain(); } catch { }
        return true;
    }

    private void MarkDirty()
    {
        if (suppressEditorEvents || string.IsNullOrWhiteSpace(activeId)) return;
        dirty = true;
        SetStatus("有未保存修改 · 约 500ms 后自动保存", Color.FromArgb(0, 127, 142));
        autoSaveTimer.Stop();
        if (NoteDraftPolicy.ShouldScheduleAutoSave(dirty)) autoSaveTimer.Start();
    }

    private bool DiscardBlankNewDraft()
    {
        if (!NoteDraftPolicy.ShouldDiscardNewDraft(activeIsNew, title.Text, body.Text)) return true;
        string error;
        if (!store.TryDiscardDraft(activeId, activeRevision, out error))
        {
            SetStatus("新便签未能取消：" + error, Color.FromArgb(204, 70, 82));
            return false;
        }
        activeId = "";
        activeRevision = 0;
        activeIsNew = false;
        dirty = false;
        suppressEditorEvents = true;
        try { title.Text = ""; category.Text = NoteCategoryPolicy.DefaultCategory; body.Text = ""; }
        finally { suppressEditorEvents = false; }
        LoadNotes("");
        SetStatus("空白新便签已取消", Color.FromArgb(96, 106, 120));
        return true;
    }

    private bool FlushPendingSave()
    {
        if (NoteDraftPolicy.ShouldDiscardNewDraft(activeIsNew, title.Text, body.Text))
            return DiscardBlankNewDraft();
        if (!dirty)
        {
            // A non-empty auto-saved draft becomes committed when the user
            // explicitly leaves the Deck; timer saves remain cancelable drafts.
            if (activeIsNew) activeIsNew = false;
            return true;
        }
        bool saved = TrySaveActive(false);
        if (saved) activeIsNew = false;
        return saved;
    }

    private void OpenAi()
    {
        if (IsRecordingGuard() || string.IsNullOrWhiteSpace(activeId) || !FlushPendingSave()) return;
        NotesLoadResult loaded = store.Load();
        NoteItem note = loaded.IsSuccess ? loaded.Document.notes.FirstOrDefault(item => item.Id == activeId) : null;
        if (note == null) { SetStatus("便签已不存在，请重新打开", Color.FromArgb(204, 70, 82)); return; }
        using (NotesAiOperationForm dialog = new NotesAiOperationForm(store, aiStore, aiService, note, this))
            dialog.ShowDialog(this);
        LoadNotes(activeId);
    }

    private void SetStatus(string text, Color color)
    {
        status.Text = text ?? "";
        status.ForeColor = color;
    }

    private void PositionToRightOfOwner()
    {
        Screen screen = owner == null ? Screen.PrimaryScreen : Screen.FromControl(owner);
        Rectangle work = screen == null ? Screen.PrimaryScreen.WorkingArea : screen.WorkingArea;
        Rectangle candidate = hasSavedLocation
            ? new Rectangle(savedLocation, Size)
            : new Rectangle(work.Right - Width - 12, work.Top + 48, Width, Height);
        Rectangle clamped = NotesDeckLayoutPolicy.ApplyToFormBounds(this, candidate, work);
        savedLocation = clamped.Location;
        hasSavedLocation = true;
    }

    private void HandleDisplaySettingsChanged(object sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(ReclampAfterDisplayChange)); } catch { }
            return;
        }
        ReclampAfterDisplayChange();
    }

    private void ReclampAfterDisplayChange()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            Screen screen = Screen.FromHandle(Handle);
            Rectangle work = screen == null ? Screen.PrimaryScreen.WorkingArea : screen.WorkingArea;
            Rectangle clamped = NotesDeckLayoutPolicy.ClampToWorkingArea(Bounds, work);
            if (fixedPanel)
            {
                fixedBounds = clamped;
                hasFixedBounds = true;
            }
            savedLocation = clamped.Location;
            hasSavedLocation = true;
            if (Bounds != clamped) Bounds = clamped;
            SavePreferences();
        }
        catch { }
    }

    private void LoadPreferences()
    {
        try
        {
            if (!File.Exists(preferencesPath)) return;
            Dictionary<string, object> raw = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(preferencesPath, Encoding.UTF8)) as Dictionary<string, object>;
            object value;
            if (raw != null && raw.TryGetValue("fixed", out value)) fixedPanel = Convert.ToBoolean(value);
            if (raw != null && raw.TryGetValue("topMost", out value)) TopMost = Convert.ToBoolean(value);
            int width;
            if (raw != null && raw.TryGetValue("width", out value) && Int32.TryParse(Convert.ToString(value), out width)) Width = Math.Max(MinimumSize.Width, Math.Min(MaximumSize.Width, width));
            int x;
            int y;
            if (raw != null && raw.TryGetValue("x", out value) && Int32.TryParse(Convert.ToString(value), out x) &&
                raw.TryGetValue("y", out value) && Int32.TryParse(Convert.ToString(value), out y))
            {
                savedLocation = new Point(x, y);
                hasSavedLocation = true;
            }
        }
        catch { fixedPanel = false; TopMost = false; hasSavedLocation = false; }
    }

    private void SavePreferences()
    {
        try
        {
            var raw = new Dictionary<string, object> { { "schemaVersion", 1 }, { "fixed", fixedPanel }, { "topMost", TopMost }, { "width", Width }, { "x", Location.X }, { "y", Location.Y } };
            string temp = preferencesPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(raw), new UTF8Encoding(false));
            if (File.Exists(preferencesPath)) File.Replace(temp, preferencesPath, preferencesPath + ".bak", true);
            else File.Move(temp, preferencesPath);
        }
        catch { }
    }
}

internal static class NotesDeckLayoutPolicy
{
    internal static Rectangle ApplyToFormBounds(Form form, Rectangle window, Rectangle workingArea)
    {
        if (form == null) throw new ArgumentNullException("form");
        Rectangle clamped = ClampToWorkingArea(window, workingArea);
        form.Bounds = clamped;
        return clamped;
    }

    internal static Rectangle ClampToWorkingArea(Rectangle window, Rectangle workingArea)
    {
        int width = Math.Min(Math.Max(1, window.Width), Math.Max(1, workingArea.Width));
        int height = Math.Min(Math.Max(1, window.Height), Math.Max(1, workingArea.Height));
        int left = Math.Max(workingArea.Left, Math.Min(window.Left, workingArea.Right - width));
        int top = Math.Max(workingArea.Top, Math.Min(window.Top, workingArea.Bottom - height));
        return new Rectangle(left, top, width, height);
    }
}

internal static class NotesDeckWindowPolicy
{
    internal static bool ShouldRestoreFixedBounds(bool fixedPanel, bool hasFixedBounds,
        Rectangle current, Rectangle expected)
    {
        return fixedPanel && hasFixedBounds && current != expected;
    }
}
