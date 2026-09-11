using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

internal sealed class CaptureAskForm : Form
{
    private readonly Form ownerForm;
    private readonly CaptureAskService service;
    private readonly WindowsCaptureAskBackend backend;
    private readonly Func<bool> recordingHasPriority;
    private readonly Action<ActionResult> publishResult;
    private readonly Action closed;
    private readonly ComboBox targetSelector = new ComboBox();
    private readonly PictureBox preview = new PictureBox();
    private readonly Label previewEmpty = new Label();
    private readonly Label stateIcon = new Label();
    private readonly Label stateText = new Label();
    private readonly Panel scrollHost;
    private readonly TableLayoutPanel root;
    private readonly Panel heading;
    private readonly FlowLayoutPanel captureActions;
    private readonly Panel previewHost;
    private readonly TableLayoutPanel targetRow;
    private readonly TableLayoutPanel statusRow;
    private readonly FlowLayoutPanel footer;
    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label targetLabel;
    private readonly Button currentWindowButton;
    private readonly Button regionButton;
    private readonly Button copyButton;
    private readonly Button pasteButton;
    private readonly Button retakeButton;
    private readonly Button cancelButton;
    private readonly System.Windows.Forms.Timer expirationTimer = new System.Windows.Forms.Timer();
    private CaptureAskPreparedImage preparedCapture;
    private bool busy;
    private bool leaveOwnerHidden;
    private bool ownerWasVisible;
    private bool darkTheme;
    private bool voiceCancellationPublished;
    private ActionState currentState = ActionState.Idle;

    internal CaptureAskForm(Form owner, CaptureAskService service, WindowsCaptureAskBackend backend,
        IList<FocusTargetDescriptor> targets, string preferredTargetId,
        Func<bool> recordingHasPriority, Action<ActionResult> publishResult, Action closed)
    {
        if (service == null) throw new ArgumentNullException("service");
        if (backend == null) throw new ArgumentNullException("backend");
        ownerForm = owner;
        this.service = service;
        this.backend = backend;
        this.recordingHasPriority = recordingHasPriority ?? delegate { return false; };
        this.publishResult = publishResult ?? delegate { };
        this.closed = closed ?? delegate { };
        ownerWasVisible = owner != null && owner.Visible;

        Text = "截图提问";
        Name = "captureAskForm";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(780, 700);
        MinimumSize = new Size(640, 560);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(245, 247, 251);
        ForeColor = Color.FromArgb(18, 30, 54);
        Font = new Font("Microsoft YaHei UI", 9.5f);
        KeyPreview = true;

        scrollHost = new Panel();
        scrollHost.Name = "captureAskScrollHost";
        scrollHost.Dock = DockStyle.Fill;
        scrollHost.AutoScroll = true;
        scrollHost.BackColor = BackColor;

        root = new TableLayoutPanel();
        root.Dock = DockStyle.Top;
        root.Height = 640;
        root.MinimumSize = new Size(0, 640);
        root.Padding = new Padding(24, 18, 24, 18);
        root.ColumnCount = 1;
        root.RowCount = 6;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));

        heading = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        titleLabel = NewLabel("截图提问", 18f, FontStyle.Bold, Color.FromArgb(18, 30, 54));
        titleLabel.Location = new Point(0, 0);
        titleLabel.Size = new Size(400, 34);
        subtitleLabel = NewLabel("截图只保存在本机；粘贴后由你补充说明并手动发送", 9f,
            FontStyle.Regular, Color.FromArgb(91, 104, 134));
        subtitleLabel.Location = new Point(1, 38);
        subtitleLabel.Size = new Size(680, 26);
        heading.Controls.Add(titleLabel);
        heading.Controls.Add(subtitleLabel);

        captureActions = new FlowLayoutPanel();
        captureActions.Dock = DockStyle.Fill;
        captureActions.FlowDirection = FlowDirection.LeftToRight;
        captureActions.WrapContents = false;
        captureActions.BackColor = Color.Transparent;
        captureActions.Padding = new Padding(0, 6, 0, 6);
        currentWindowButton = NewButton("当前窗口", true, 126);
        currentWindowButton.Name = "captureCurrentWindowButton";
        currentWindowButton.Click += delegate { BeginCurrentWindowCapture(); };
        regionButton = NewButton("选择区域", false, 126);
        regionButton.Name = "captureRegionButton";
        regionButton.Click += delegate { BeginRegionCapture(); };
        captureActions.Controls.Add(currentWindowButton);
        captureActions.Controls.Add(regionButton);

        previewHost = new Panel();
        previewHost.Dock = DockStyle.Fill;
        previewHost.Margin = new Padding(0, 4, 0, 10);
        previewHost.Padding = new Padding(1);
        previewHost.BackColor = Color.FromArgb(220, 226, 239);
        preview.Name = "captureAskPreview";
        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.FromArgb(233, 237, 245);
        preview.SizeMode = PictureBoxSizeMode.Zoom;
        previewEmpty.Text = "尚未截图";
        previewEmpty.Dock = DockStyle.Fill;
        previewEmpty.TextAlign = ContentAlignment.MiddleCenter;
        previewEmpty.ForeColor = Color.FromArgb(91, 104, 134);
        previewEmpty.BackColor = Color.FromArgb(248, 250, 253);
        previewHost.Controls.Add(preview);
        previewHost.Controls.Add(previewEmpty);
        previewEmpty.BringToFront();

        targetRow = new TableLayoutPanel();
        targetRow.Dock = DockStyle.Fill;
        targetRow.ColumnCount = 2;
        targetRow.RowCount = 1;
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        targetLabel = NewLabel("发送到目标", 9.5f, FontStyle.Bold, Color.FromArgb(18, 30, 54));
        targetLabel.Dock = DockStyle.Fill;
        targetLabel.TextAlign = ContentAlignment.MiddleLeft;
        targetSelector.Name = "captureAskTarget";
        targetSelector.Dock = DockStyle.Fill;
        targetSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        targetSelector.FlatStyle = FlatStyle.Flat;
        targetSelector.Margin = new Padding(0, 9, 0, 9);
        targetSelector.Font = new Font("Microsoft YaHei UI", 9.5f);
        targetRow.Controls.Add(targetLabel, 0, 0);
        targetRow.Controls.Add(targetSelector, 1, 0);

        statusRow = new TableLayoutPanel();
        statusRow.Dock = DockStyle.Fill;
        statusRow.BackColor = Color.FromArgb(238, 241, 248);
        statusRow.Padding = new Padding(14, 8, 14, 8);
        statusRow.ColumnCount = 2;
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38f));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        stateIcon.Name = "captureAskStateIcon";
        stateIcon.Text = "\uE946";
        stateIcon.Font = UiFonts.Icon(14f);
        stateIcon.ForeColor = Color.FromArgb(104, 82, 244);
        stateIcon.Dock = DockStyle.Fill;
        stateIcon.TextAlign = ContentAlignment.MiddleCenter;
        stateText.Name = "captureAskState";
        stateText.Text = "选择当前窗口或区域开始截图";
        stateText.Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold);
        stateText.ForeColor = Color.FromArgb(91, 104, 134);
        stateText.Dock = DockStyle.Fill;
        stateText.TextAlign = ContentAlignment.MiddleLeft;
        stateText.AutoEllipsis = true;
        statusRow.Controls.Add(stateIcon, 0, 0);
        statusRow.Controls.Add(stateText, 1, 0);

        footer = new FlowLayoutPanel();
        footer.Dock = DockStyle.Fill;
        footer.FlowDirection = FlowDirection.RightToLeft;
        footer.WrapContents = false;
        footer.Padding = new Padding(0, 6, 0, 0);
        footer.BackColor = Color.Transparent;
        pasteButton = NewButton("粘贴到目标", true, 134);
        pasteButton.Name = "captureAskPasteButton";
        pasteButton.Enabled = false;
        pasteButton.Click += delegate { PastePreparedCapture(); };
        copyButton = NewButton("复制截图", false, 110);
        copyButton.Name = "captureAskCopyButton";
        copyButton.Enabled = false;
        copyButton.Click += delegate { CopyPreparedCapture(); };
        retakeButton = NewButton("重新截图", false, 110);
        retakeButton.Name = "captureAskRetakeButton";
        retakeButton.Enabled = false;
        retakeButton.Click += delegate { ResetPreparedCapture(); };
        cancelButton = NewButton("取消", false, 88);
        cancelButton.Name = "captureAskCancelButton";
        cancelButton.Click += delegate { CancelAndClose(); };
        footer.Controls.Add(pasteButton);
        footer.Controls.Add(copyButton);
        footer.Controls.Add(retakeButton);
        footer.Controls.Add(cancelButton);
        targetSelector.SelectedIndexChanged += delegate
        {
            pasteButton.Enabled = !busy && preparedCapture != null && targetSelector.SelectedItem != null;
        };

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(captureActions, 0, 1);
        root.Controls.Add(previewHost, 0, 2);
        root.Controls.Add(targetRow, 0, 3);
        root.Controls.Add(statusRow, 0, 4);
        root.Controls.Add(footer, 0, 5);
        scrollHost.Controls.Add(root);
        Controls.Add(scrollHost);
        scrollHost.Resize += delegate { LayoutScrollableContent(); };

        ApplyTheme(false);
        PopulateTargets(targets, preferredTargetId);
        expirationTimer.Interval = 10 * 60 * 1000;
        expirationTimer.Tick += delegate
        {
            expirationTimer.Stop();
            service.CancelCurrent();
            publishResult(ActionResult.Create("截图提问", "本地截图", ActionState.Canceled,
                "截图提问已超时取消，临时图片已清理", "长时间没有继续操作",
                "需要时重新截图", "CAPTURE-ASK-TIMEOUT"));
            Close();
        };
        expirationTimer.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyWorkingAreaLayout(Screen.FromControl(this).WorkingArea);
    }

    private void ApplyWorkingAreaLayout(Rectangle workingArea)
    {
        Size scaledMinimum = MinimumSize;
        Size fitted = FitWindowToWorkingArea(Size, workingArea);
        MinimumSize = Size.Empty;
        Size = fitted;
        MinimumSize = new Size(Math.Min(scaledMinimum.Width, fitted.Width),
            Math.Min(scaledMinimum.Height, fitted.Height));
        Location = new Point(workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
            workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2));
        LayoutScrollableContent();
    }

    private void LayoutScrollableContent()
    {
        if (scrollHost == null || root == null) return;
        root.Width = Math.Max(1, scrollHost.ClientSize.Width);
        root.Height = Math.Max(scrollHost.ClientSize.Height, root.MinimumSize.Height);
    }

    private static Size FitWindowToWorkingArea(Size desired, Rectangle workingArea)
    {
        const int margin = 24;
        int availableWidth = Math.Max(1, workingArea.Width - margin);
        int availableHeight = Math.Max(1, workingArea.Height - margin);
        return new Size(Math.Min(Math.Max(1, desired.Width), availableWidth),
            Math.Min(Math.Max(1, desired.Height), availableHeight));
    }

    private void PopulateTargets(IList<FocusTargetDescriptor> targets, string preferredTargetId)
    {
        int selected = -1;
        if (targets != null)
        {
            foreach (FocusTargetDescriptor target in targets)
            {
                if (target == null) continue;
                var choice = new CaptureAskTargetChoice(target);
                int index = targetSelector.Items.Add(choice);
                if (string.Equals(target.Id, preferredTargetId, StringComparison.OrdinalIgnoreCase))
                    selected = index;
            }
        }
        if (selected >= 0) targetSelector.SelectedIndex = selected;
        if (targetSelector.Items.Count == 0)
            SetState(ActionState.Warning, "没有已验证输入目标；可以截图和复制，但不会执行粘贴");
    }

    private void BeginCurrentWindowCapture()
    {
        if (!CanBeginCapture()) return;
        SetBusy(true);
        Publish(ActionResult.Create("截图提问", "当前窗口", ActionState.Running,
            "正在获取当前窗口截图", "", "", ""));
        SetState(ActionState.Running, "正在获取当前窗口截图...");
        HideForExternalAction();
        ThreadPool.QueueUserWorkItem(delegate
        {
            CaptureAskScreenResult captured = backend.CaptureRecentWindow(
                ownerForm == null ? IntPtr.Zero : ownerForm.Handle, 1800, HasRecordingPriority);
            if (!PostToUi(delegate { CompleteWindowCapture(captured); })) captured.Dispose();
        });
    }

    private void CompleteWindowCapture(CaptureAskScreenResult captured)
    {
        try
        {
            if (IsDisposed || Disposing || AbortCaptureCompletionForRecording(null)) return;
            if (captured == null || !captured.IsSuccess)
            {
                string code = captured == null ? "CAPTURE-ASK-SCREEN-FAILED" : captured.ErrorCode;
                HandleCaptureFailure(code);
                return;
            }
            CaptureAskPrepareResult prepared = service.Prepare(captured.Source,
                captured.SelectedBounds, "window");
            if (!prepared.IsSuccess)
            {
                HandleCaptureFailure(prepared.ErrorCode);
                return;
            }
            if (AbortCaptureCompletionForRecording(prepared.Capture)) return;
            SetPreparedCapture(prepared.Capture);
            if (AbortCaptureCompletionForRecording(null)) return;
            RestoreCaptureForm();
            SetBusy(false);
            ActionResult result = ActionResult.Create("截图提问", "当前窗口", ActionState.Success,
                "截图已准备，可复制或粘贴到已验证目标", "", "", "");
            SetState(result.State, result.Message);
            Publish(result);
        }
        finally
        {
            if (captured != null) captured.Dispose();
        }
    }

    private void BeginRegionCapture()
    {
        if (!CanBeginCapture()) return;
        SetBusy(true);
        Publish(ActionResult.Create("截图提问", "选择区域", ActionState.Running,
            "正在准备区域截图", "", "", ""));
        SetState(ActionState.Running, "正在准备区域截图...");
        HideForExternalAction();
        Application.DoEvents();
        using (CaptureAskScreenResult captured = backend.CaptureVirtualScreen(HasRecordingPriority))
        {
            if (captured == null || !captured.IsSuccess)
            {
                HandleCaptureFailure(captured == null ? "CAPTURE-ASK-SCREEN-FAILED" : captured.ErrorCode);
                return;
            }
            using (var selector = new CaptureAskRegionForm(captured.Source,
                captured.SourceBounds, HasRecordingPriority))
            {
                DialogResult selection = selector.ShowDialog();
                if (selection != DialogResult.OK)
                {
                    HandleCaptureFailure(selector.VoiceCanceled
                        ? "CAPTURE-ASK-CANCELED-VOICE" : "CAPTURE-ASK-CANCELED");
                    return;
                }
                if (AbortCaptureCompletionForRecording(null)) return;
                Rectangle relative;
                if (!CaptureAskGeometry.TryToRelativeSelection(captured.SourceBounds,
                    selector.SelectedAbsoluteBounds, out relative))
                {
                    HandleCaptureFailure("CAPTURE-ASK-BOUNDS-INVALID");
                    return;
                }
                CaptureAskPrepareResult prepared = service.Prepare(captured.Source, relative, "region");
                if (!prepared.IsSuccess)
                {
                    HandleCaptureFailure(prepared.ErrorCode);
                    return;
                }
                if (AbortCaptureCompletionForRecording(prepared.Capture)) return;
                SetPreparedCapture(prepared.Capture);
            }
        }
        if (AbortCaptureCompletionForRecording(null)) return;
        RestoreCaptureForm();
        SetBusy(false);
        ActionResult result = ActionResult.Create("截图提问", "选择区域", ActionState.Success,
            "截图已准备，可复制或粘贴到已验证目标", "", "", "");
        SetState(result.State, result.Message);
        Publish(result);
    }

    private bool CanBeginCapture()
    {
        if (busy) return false;
        if (!HasRecordingPriority()) return true;
        ActionResult result = ActionResult.Create("截图提问", "屏幕截图", ActionState.Canceled,
            "录音正在进行，本次未截图", "录音操作优先",
            "录音结束后重试", "CAPTURE-ASK-CANCELED-VOICE");
        SetState(result.State, result.Message);
        Publish(result);
        return false;
    }

    private bool AbortCaptureCompletionForRecording(CaptureAskPreparedImage unownedCapture)
    {
        if (!HasRecordingPriority()) return false;
        if (unownedCapture != null) service.Cleanup(unownedCapture);
        if (!IsDisposed && !Disposing) HandleCaptureFailure("CAPTURE-ASK-CANCELED-VOICE");
        return true;
    }

    private void CopyPreparedCapture()
    {
        if (busy || preparedCapture == null) return;
        SetBusy(true);
        SetState(ActionState.Running, "正在把本次截图写入剪贴板...");
        ActionResult result = service.CopyToClipboard(preparedCapture);
        SetBusy(false);
        SetState(result.State, ResultText(result));
        Publish(result);
    }

    private void PastePreparedCapture()
    {
        if (busy || preparedCapture == null) return;
        CaptureAskTargetChoice choice = targetSelector.SelectedItem as CaptureAskTargetChoice;
        if (choice == null)
        {
            ActionResult missing = ActionResult.Create("截图提问", "未设置", ActionState.Error,
                "未找到已验证输入目标，未执行粘贴", "没有选择可用的 AI 输入目标",
                "先设置并测试输入目标", "FOCUS-TARGET-MISSING");
            SetState(missing.State, ResultText(missing));
            Publish(missing);
            return;
        }
        if (!CanBeginCapture()) return;
        SetBusy(true);
        ActionResult running = ActionResult.Create("截图提问", choice.Target.Name, ActionState.Running,
            "正在锁定目标并准备图片粘贴", "", "", "");
        SetState(running.State, running.Message);
        Publish(running);
        HideForExternalAction();
        CaptureAskPreparedImage capture = preparedCapture;
        FocusTargetDescriptor target = choice.Target.Copy();
        var worker = new Thread(new ThreadStart(delegate
        {
            ActionResult result = service.PasteToTarget(capture, target, 5000);
            PostToUi(delegate { CompletePaste(result); });
        }));
        worker.IsBackground = true;
        worker.Name = "Vibe Flow Capture Ask paste";
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    private void CompletePaste(ActionResult result)
    {
        bool voiceCanceled = result != null && string.Equals(result.ErrorCode,
            "CAPTURE-ASK-CANCELED-VOICE", StringComparison.OrdinalIgnoreCase);
        if (voiceCanceled) PublishVoiceCancellationOnce();
        else Publish(result);
        if (result != null && (result.IsSuccess || voiceCanceled))
        {
            leaveOwnerHidden = true;
            Close();
            return;
        }
        RestoreCaptureForm();
        SetBusy(false);
        SetState(result == null ? ActionState.Error : result.State, ResultText(result));
    }

    private void ResetPreparedCapture()
    {
        if (busy) return;
        ReleasePreparedCapture();
        previewEmpty.Visible = true;
        copyButton.Enabled = false;
        pasteButton.Enabled = false;
        retakeButton.Enabled = false;
        SetState(ActionState.Idle, "选择当前窗口或区域开始截图");
    }

    private void SetPreparedCapture(CaptureAskPreparedImage capture)
    {
        ReleasePreparedCapture();
        preparedCapture = capture;
        preview.Image = preparedCapture.CreateImageCopy();
        previewEmpty.Visible = false;
        copyButton.Enabled = true;
        pasteButton.Enabled = targetSelector.SelectedItem != null;
        retakeButton.Enabled = true;
        expirationTimer.Stop();
        expirationTimer.Start();
    }

    private void HandleCaptureFailure(string errorCode)
    {
        bool voice = string.Equals(errorCode, "CAPTURE-ASK-CANCELED-VOICE",
            StringComparison.OrdinalIgnoreCase);
        if (voice)
        {
            PublishVoiceCancellationOnce();
            leaveOwnerHidden = true;
            if (!IsDisposed && !Disposing) Close();
            return;
        }
        bool canceled = voice || string.Equals(errorCode, "CAPTURE-ASK-CANCELED",
            StringComparison.OrdinalIgnoreCase);
        ActionResult result = ActionResult.Create("截图提问", "屏幕截图",
            canceled ? ActionState.Canceled : ActionState.Error,
            voice ? "录音已开始，截图提问已取消" : canceled ? "已取消截图，未保存图片" :
                "未能生成截图，本次未写入剪贴板",
            voice ? "录音操作优先" : canceled ? "用户取消了区域选择" : CaptureFailureReason(errorCode),
            voice ? "录音结束后重新截图" : canceled ? "需要时重新截图" : "切换到目标窗口后重试",
            errorCode);
        Publish(result);
        RestoreCaptureForm();
        SetBusy(false);
        SetState(result.State, ResultText(result));
    }

    private void CancelAndClose()
    {
        service.CancelCurrent();
        Publish(ActionResult.Create("截图提问", "本地截图", ActionState.Canceled,
            "截图提问已取消，未执行后续步骤", "用户取消了本次操作",
            "需要时重新截图", "CAPTURE-ASK-CANCELED"));
        Close();
    }

    internal void HandleRecordingStarted()
    {
        if (IsDisposed || Disposing) return;
        leaveOwnerHidden = true;
        PublishVoiceCancellationOnce();
        Close();
    }

    private void PublishVoiceCancellationOnce()
    {
        if (voiceCancellationPublished) return;
        voiceCancellationPublished = true;
        Publish(ActionResult.Create("截图提问", "本地截图", ActionState.Canceled,
            "录音已开始，截图提问已取消", "录音操作优先",
            "录音结束后重新截图", "CAPTURE-ASK-CANCELED-VOICE"));
    }

    internal void CloseForOwnerShutdown()
    {
        if (IsDisposed || Disposing) return;
        leaveOwnerHidden = true;
        Close();
    }

    private void HideForExternalAction()
    {
        backend.ObserveForegroundWindow();
        Hide();
        if (ownerForm != null && !ownerForm.IsDisposed && ownerForm.Visible) ownerForm.Hide();
        Application.DoEvents();
    }

    private void RestoreCaptureForm()
    {
        if (IsDisposed || Disposing) return;
        Show();
        Activate();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        currentWindowButton.Enabled = !value;
        regionButton.Enabled = !value;
        copyButton.Enabled = !value && preparedCapture != null;
        pasteButton.Enabled = !value && preparedCapture != null && targetSelector.SelectedItem != null;
        retakeButton.Enabled = !value && preparedCapture != null;
        targetSelector.Enabled = !value;
        cancelButton.Enabled = true;
    }

    private bool HasRecordingPriority()
    {
        try { return recordingHasPriority(); }
        catch { return true; }
    }

    private void SetState(ActionState state, string text)
    {
        currentState = state;
        Color color = state == ActionState.Success ?
            (darkTheme ? Color.FromArgb(76, 174, 127) : Color.FromArgb(10, 150, 95)) :
            state == ActionState.Error ?
            (darkTheme ? Color.FromArgb(205, 101, 110) : Color.FromArgb(204, 70, 82)) :
            state == ActionState.Warning || state == ActionState.Canceled ?
            (darkTheme ? Color.FromArgb(205, 157, 81) : Color.FromArgb(201, 128, 24)) :
            (darkTheme ? Color.FromArgb(126, 118, 213) : Color.FromArgb(104, 82, 244));
        stateIcon.Text = state == ActionState.Success ? "\uE73E" : state == ActionState.Error ? "\uEA39" :
            state == ActionState.Running || state == ActionState.Checking ? "\uE895" : "\uE946";
        stateIcon.ForeColor = color;
        stateText.ForeColor = color;
        stateText.Text = text ?? "";
    }

    internal void ApplyTheme(bool useDarkTheme)
    {
        darkTheme = useDarkTheme;
        Color ink = darkTheme ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
        Color muted = darkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(91, 104, 134);
        Color primary = darkTheme ? Color.FromArgb(126, 118, 213) : Color.FromArgb(104, 82, 244);
        Color page = darkTheme ? Color.FromArgb(25, 26, 31) : Color.FromArgb(245, 247, 251);
        Color card = darkTheme ? Color.FromArgb(35, 37, 44) : Color.FromArgb(248, 250, 253);
        Color surface = darkTheme ? Color.FromArgb(41, 43, 51) : Color.White;
        Color input = darkTheme ? Color.FromArgb(31, 33, 39) : Color.White;
        Color border = darkTheme ? Color.FromArgb(55, 59, 69) : Color.FromArgb(220, 226, 239);

        BackColor = page;
        ForeColor = ink;
        scrollHost.BackColor = page;
        root.BackColor = page;
        heading.BackColor = Color.Transparent;
        captureActions.BackColor = Color.Transparent;
        targetRow.BackColor = page;
        footer.BackColor = Color.Transparent;
        previewHost.BackColor = border;
        preview.BackColor = card;
        previewEmpty.BackColor = card;
        previewEmpty.ForeColor = muted;
        statusRow.BackColor = darkTheme ? Color.FromArgb(38, 40, 48) : Color.FromArgb(238, 241, 248);
        titleLabel.ForeColor = ink;
        subtitleLabel.ForeColor = muted;
        targetLabel.ForeColor = ink;
        targetSelector.BackColor = input;
        targetSelector.ForeColor = ink;

        ApplyButtonTheme(currentWindowButton, true, ink, primary, surface, border, darkTheme);
        ApplyButtonTheme(regionButton, false, ink, primary, surface, border, darkTheme);
        ApplyButtonTheme(copyButton, false, ink, primary, surface, border, darkTheme);
        ApplyButtonTheme(pasteButton, true, ink, primary, surface, border, darkTheme);
        ApplyButtonTheme(retakeButton, false, ink, primary, surface, border, darkTheme);
        ApplyButtonTheme(cancelButton, false, ink, primary, surface, border, darkTheme);
        SetState(currentState, stateText.Text);
    }

    private static void ApplyButtonTheme(Button button, bool primaryButton, Color ink,
        Color primary, Color surface, Color border, bool useDarkTheme)
    {
        if (button == null) return;
        button.BackColor = primaryButton ? primary : surface;
        button.ForeColor = primaryButton ? Color.White : ink;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = primaryButton
            ? (useDarkTheme ? Color.FromArgb(142, 135, 226) : Color.FromArgb(88, 66, 238))
            : (useDarkTheme ? Color.FromArgb(47, 49, 57) : Color.FromArgb(232, 236, 255));
        button.FlatAppearance.MouseDownBackColor = primaryButton
            ? (useDarkTheme ? Color.FromArgb(158, 152, 233) : Color.FromArgb(72, 52, 220))
            : (useDarkTheme ? Color.FromArgb(55, 58, 68) : Color.FromArgb(219, 225, 252));
        if (button.Tag == null)
        {
            button.Tag = "rounded-feedback";
            Action updateRegion = delegate
            {
                if (button.Width <= 0 || button.Height <= 0) return;
                Region previous = button.Region;
                using (var path = RoundedButtonPath(new Rectangle(0, 0, button.Width, button.Height), 6))
                    button.Region = new Region(path);
                if (previous != null) previous.Dispose();
            };
            button.Resize += delegate { updateRegion(); };
            updateRegion();
        }
    }

    private static GraphicsPath RoundedButtonPath(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void Publish(ActionResult result)
    {
        try { publishResult(result); } catch { }
    }

    private bool PostToUi(Action action)
    {
        if (action == null || IsDisposed || Disposing || !IsHandleCreated) return false;
        try { BeginInvoke(action); return true; }
        catch { return false; }
    }

    private void ReleasePreparedCapture()
    {
        Image oldPreview = preview.Image;
        preview.Image = null;
        if (oldPreview != null) oldPreview.Dispose();
        if (preparedCapture != null) service.Cleanup(preparedCapture);
        preparedCapture = null;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        expirationTimer.Stop();
        service.CancelCurrent();
        ReleasePreparedCapture();
        if (!leaveOwnerHidden && ownerWasVisible && !HasRecordingPriority() && ownerForm != null &&
            !ownerForm.IsDisposed && !ownerForm.Disposing)
        {
            ownerForm.Show();
            ownerForm.Activate();
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        expirationTimer.Dispose();
        try { closed(); } catch { }
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            CancelAndClose();
            return;
        }
        base.OnKeyDown(e);
    }

    private static string CaptureFailureReason(string code)
    {
        if (code == "CAPTURE-ASK-WINDOW-NOT-FOUND") return "没有找到可截图的前台应用窗口";
        if (code == "CAPTURE-ASK-WINDOW-BOUNDS-FAILED") return "前台窗口没有可见截图区域";
        if (code == "CAPTURE-ASK-BOUNDS-INVALID") return "选择区域超出当前屏幕范围";
        return "Windows 屏幕捕获暂时不可用";
    }

    private static string ResultText(ActionResult result)
    {
        if (result == null) return "截图提问失败。";
        string text = result.Message;
        if (!string.IsNullOrWhiteSpace(result.ErrorReason)) text += " 原因：" + result.ErrorReason;
        if (!string.IsNullOrWhiteSpace(result.RecoveryAction)) text += " 下一步：" + result.RecoveryAction;
        if (!string.IsNullOrWhiteSpace(result.ErrorCode)) text += " 错误码：" + result.ErrorCode;
        return text;
    }

    private static Label NewLabel(string text, float size, FontStyle style, Color color)
    {
        return new Label { Text = text, Font = new Font("Microsoft YaHei UI", size, style),
            ForeColor = color, BackColor = Color.Transparent };
    }

    private static Button NewButton(string text, bool primary, int width)
    {
        var button = new Button();
        button.Text = text;
        button.Size = new Size(width, 40);
        button.Margin = new Padding(0, 0, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(206, 214, 230);
        button.BackColor = primary ? Color.FromArgb(104, 82, 244) : Color.White;
        button.ForeColor = primary ? Color.White : Color.FromArgb(18, 30, 54);
        button.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        return button;
    }
}

internal sealed class CaptureAskTargetChoice
{
    internal FocusTargetDescriptor Target { get; private set; }

    internal CaptureAskTargetChoice(FocusTargetDescriptor target)
    {
        Target = target == null ? null : target.Copy();
    }

    public override string ToString()
    {
        return Target == null ? "未设置" : Target.Name + "  ·  " +
            OverlayText.SafeProcessName(Target.ProcessName);
    }
}

internal sealed class CaptureAskRegionForm : Form
{
    private readonly Bitmap source;
    private readonly Rectangle sourceBounds;
    private readonly Func<bool> recordingHasPriority;
    private readonly System.Windows.Forms.Timer recordingTimer = new System.Windows.Forms.Timer();
    private Point dragStart;
    private Rectangle selection;
    private bool dragging;

    internal Rectangle SelectedAbsoluteBounds { get; private set; }
    internal bool VoiceCanceled { get; private set; }

    internal CaptureAskRegionForm(Bitmap source, Rectangle sourceBounds,
        Func<bool> recordingHasPriority)
    {
        if (source == null) throw new ArgumentNullException("source");
        this.source = source;
        this.sourceBounds = sourceBounds;
        this.recordingHasPriority = recordingHasPriority ?? delegate { return false; };
        Text = "选择截图区域";
        Name = "captureAskRegionForm";
        AccessibleName = "选择截图区域";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = sourceBounds;
        ShowInTaskbar = true;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        recordingTimer.Interval = 80;
        recordingTimer.Tick += delegate
        {
            if (!HasRecordingPriority()) return;
            VoiceCanceled = true;
            DialogResult = DialogResult.Cancel;
            Close();
        };
        recordingTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(source, Point.Empty);
        using (var shade = new SolidBrush(Color.FromArgb(118, 8, 14, 26)))
            e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.Width > 0 && selection.Height > 0)
        {
            e.Graphics.DrawImage(source, selection, selection, GraphicsUnit.Pixel);
            using (var outline = new Pen(Color.FromArgb(89, 216, 229), 2f))
            {
                outline.DashStyle = DashStyle.Solid;
                e.Graphics.DrawRectangle(outline, selection);
            }
        }
        using (var panel = new SolidBrush(Color.FromArgb(220, 18, 30, 54)))
            e.Graphics.FillRectangle(panel, 24, 24, 300, 54);
        using (var font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold))
        using (var textBrush = new SolidBrush(Color.White))
            e.Graphics.DrawString("拖动选择问题区域 · Esc 取消", font, textBrush, 42, 41);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        dragging = true;
        dragStart = e.Location;
        selection = Rectangle.Empty;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging) return;
        selection = NormalizeRectangle(dragStart, e.Location, ClientRectangle);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        dragging = false;
        selection = NormalizeRectangle(dragStart, e.Location, ClientRectangle);
        if (selection.Width < 8 || selection.Height < 8)
        {
            selection = Rectangle.Empty;
            Invalidate();
            return;
        }
        SelectedAbsoluteBounds = new Rectangle(sourceBounds.Left + selection.Left,
            sourceBounds.Top + selection.Top, selection.Width, selection.Height);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        recordingTimer.Stop();
        recordingTimer.Dispose();
        base.OnFormClosed(e);
    }

    private bool HasRecordingPriority()
    {
        try { return recordingHasPriority(); }
        catch { return true; }
    }

    private static Rectangle NormalizeRectangle(Point start, Point end, Rectangle limit)
    {
        int left = Math.Max(limit.Left, Math.Min(start.X, end.X));
        int top = Math.Max(limit.Top, Math.Min(start.Y, end.Y));
        int right = Math.Min(limit.Right, Math.Max(start.X, end.X));
        int bottom = Math.Min(limit.Bottom, Math.Max(start.Y, end.Y));
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
