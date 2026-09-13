using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class LiveHudForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_SHOWNOACTIVATE = 4;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private readonly Font titleFont = new Font(UiDesignTokens.FeedbackFontFamily,
        UiDesignTokens.FeedbackPanelTitleSize, FontStyle.Bold);
    private readonly Font detailFont = new Font(UiDesignTokens.FeedbackFontFamily,
        UiDesignTokens.FeedbackDetailSize, FontStyle.Regular);
    private readonly Font glyphFont = UiFonts.Icon(15f, FontStyle.Regular);
    private readonly Font captionFont = new Font(UiDesignTokens.FeedbackFontFamily,
        UiDesignTokens.FeedbackCaptionSize, FontStyle.Bold);
    private readonly Font closeFont = UiFonts.Icon(11f, FontStyle.Regular);
    private readonly Label stateGlyph = new Label();
    private readonly Label titleLabel = new Label();
    private readonly Label detailLabel = new Label();
    private readonly Label contextLabel = new Label();
    private readonly Label closeLabel = new Label();
    private readonly Label meterCaption = new Label();
    private readonly RmsMeterPanel meter = new RmsMeterPanel();
    private readonly Panel border = new Panel();
    private readonly TableLayoutPanel surface = new TableLayoutPanel();
    private readonly System.Windows.Forms.Timer glowTimer = new System.Windows.Forms.Timer();
    private double audioRmsPercent;
    private Color accent = Color.FromArgb(104, 82, 244);
    private float glowPhase;
    private float glowSpeed = 0.12f;
    private float glowStrength = 0.5f;
    private int formRadius = UiDesignTokens.FeedbackRadiusPanel;
    internal event EventHandler Dismissed;

    internal LiveHudForm()
    {
        UiDisplayScale.Apply(this);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(400, 160);
        MinimumSize = new Size(340, 140);
        TopMost = true;
        ShowInTaskbar = false;
        Opacity = 0.88;
        AccessibleName = "Live HUD 状态浮层";
        Resize += delegate { ApplyFormRegion(); };

        glowTimer.Interval = 60;
        glowTimer.Tick += delegate
        {
            // State-indicator light wave only: it visualizes the CURRENT STATE
            // (recording/processing/error...), never audio. The meter remains
            // the single source of audio evidence and only ever shows measured
            // values.
            glowPhase += glowSpeed;
            if (glowPhase > 62.8f) glowPhase -= 62.8f;
            Invalidate(true);
            stateGlyph.Invalidate();
        };

        border.Dock = DockStyle.Fill;
        border.Padding = new Padding(1);

        surface.Dock = DockStyle.Fill;
        surface.Padding = new Padding(18, 12, 12, 10);
        surface.ColumnCount = 3;
        surface.RowCount = 5;
        surface.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44f));
        surface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        surface.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f));
        surface.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        surface.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
        surface.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));
        surface.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        surface.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        surface.Paint += PaintSurfaceTexture;

        stateGlyph.Dock = DockStyle.Fill;
        stateGlyph.Text = "\uE946";
        stateGlyph.Font = glyphFont;
        stateGlyph.ForeColor = accent;
        stateGlyph.TextAlign = ContentAlignment.MiddleCenter;
        stateGlyph.TabStop = false;
        stateGlyph.AccessibleName = "状态图标";
        stateGlyph.BackColor = Color.Transparent;
        stateGlyph.Paint += PaintStateGlyphChip;
        stateGlyph.Resize += delegate { ApplyGlyphRegion(); };

        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = titleFont;
        titleLabel.ForeColor = Color.FromArgb(232, 236, 246);
        titleLabel.BackColor = Color.Transparent;
        titleLabel.Text = "正在检查连接";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.AutoEllipsis = true;
        titleLabel.TabStop = false;

        closeLabel.Dock = DockStyle.Fill;
        closeLabel.Font = closeFont;
        closeLabel.BackColor = Color.Transparent;
        closeLabel.ForeColor = Color.FromArgb(150, 158, 178);
        closeLabel.Text = "\uE8BB";
        closeLabel.TextAlign = ContentAlignment.MiddleCenter;
        closeLabel.Cursor = Cursors.Hand;
        closeLabel.TabStop = false;
        closeLabel.AccessibleName = "关闭状态浮层";
        closeLabel.Click += delegate
        {
            HideInactive();
            EventHandler handler = Dismissed;
            if (handler != null) handler(this, EventArgs.Empty);
        };

        detailLabel.Dock = DockStyle.Fill;
        detailLabel.Font = detailFont;
        detailLabel.ForeColor = Color.FromArgb(196, 203, 220);
        detailLabel.BackColor = Color.Transparent;
        detailLabel.Text = "等待状态回执";
        detailLabel.TextAlign = ContentAlignment.MiddleLeft;
        detailLabel.AutoEllipsis = true;
        detailLabel.TabStop = false;

        meterCaption.Dock = DockStyle.Fill;
        meterCaption.Font = captionFont;
        meterCaption.BackColor = Color.Transparent;
        meterCaption.ForeColor = Color.FromArgb(158, 166, 186);
        meterCaption.TextAlign = ContentAlignment.MiddleLeft;
        meterCaption.AutoEllipsis = true;
        meterCaption.TabStop = false;
        meter.Dock = DockStyle.Fill;
        meter.TabStop = false;
        meter.AccessibleName = "真实音频峰值指示";

        contextLabel.Dock = DockStyle.Fill;
        contextLabel.Font = detailFont;
        contextLabel.ForeColor = Color.FromArgb(158, 166, 186);
        contextLabel.BackColor = Color.Transparent;
        contextLabel.Text = "Profile · 通用导航  ·  目标 · 未设置";
        contextLabel.TextAlign = ContentAlignment.MiddleLeft;
        contextLabel.AutoEllipsis = true;
        contextLabel.TabStop = false;

        surface.Controls.Add(stateGlyph, 0, 0);
        surface.SetRowSpan(stateGlyph, 2);
        surface.Controls.Add(titleLabel, 1, 0);
        surface.Controls.Add(closeLabel, 2, 0);
        surface.Controls.Add(detailLabel, 1, 1);
        surface.SetColumnSpan(detailLabel, 2);
        surface.Controls.Add(meterCaption, 1, 2);
        surface.SetColumnSpan(meterCaption, 2);
        surface.Controls.Add(meter, 1, 3);
        surface.SetColumnSpan(meter, 2);
        surface.Controls.Add(contextLabel, 0, 4);
        surface.SetColumnSpan(contextLabel, 3);
        border.Controls.Add(surface);
        Controls.Add(border);
        ApplyTheme(false);
        ApplyGlyphRegion();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return parameters;
        }
    }

    private void PaintSurfaceTexture(object sender, PaintEventArgs e)
    {
        var panel = (Control)sender;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // Liquid-glass surface: a soft violet→cyan wash on dark glass, a
        // sweeping brand light-wave along the top, the left accent rail, and a
        // restrained dot grid. Decorative only; the light wave encodes state
        // activity, never audio.
        using (var wash = new LinearGradientBrush(
            new Rectangle(0, 0, panel.Width, panel.Height),
            Color.FromArgb(26, 88, 60, 224), Color.FromArgb(18, 0, 153, 190), 0f))
            e.Graphics.FillRectangle(wash, panel.ClientRectangle);
        double wave01 = 0.5 + 0.5 * Math.Sin(glowPhase);
        int bandX = (int)(((glowPhase / 6.2831f) % 1.0f) * panel.Width);
        using (var wave = new LinearGradientBrush(
            new Rectangle(bandX - 100, 0, 200, 3),
            Color.FromArgb(0, 124, 100, 255),
            Color.FromArgb((int)(170 * glowStrength * (0.4 + 0.6 * wave01)), 124, 100, 255), 0f))
            e.Graphics.FillRectangle(wave, bandX - 100, 0, 200, 3);
        using (var hair = new Pen(Color.FromArgb(10, 158, 170, 255), 1f))
        {
            for (int x = panel.Width - 34; x < panel.Width - 10; x += 24)
                e.Graphics.DrawLine(hair, x, 18, x, panel.Height - 14);
        }
        using (var strokePen = new Pen(Color.FromArgb(
            (int)(64 * glowStrength * (0.5 + 0.5 * wave01)), 124, 100, 255), 1f))
        using (GraphicsPath strokePath = RoundedPath(new Rectangle(1, 1, panel.Width - 3, panel.Height - 3), formRadius - 2))
            e.Graphics.DrawPath(strokePen, strokePath);
    }

    private void ApplyFormRegion()
    {
        if (Width <= 0 || Height <= 0 || IsDisposed) return;
        Region previous = Region;
        using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, Width, Height), formRadius))
            Region = new Region(path);
        if (previous != null) previous.Dispose();
    }

    private void ApplyGlyphRegion()
    {
        if (stateGlyph.Width <= 0 || stateGlyph.Height <= 0) return;
        Region previous = stateGlyph.Region;
        using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, stateGlyph.Width - 1, stateGlyph.Height - 1), 8))
            stateGlyph.Region = new Region(path);
        if (previous != null) previous.Dispose();
    }

    private void PaintStateGlyphChip(object sender, PaintEventArgs e)
    {
        Control control = (Control)sender;
        if (control.Width <= 0 || control.Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle chip = new Rectangle(1, 1, control.Width - 3, control.Height - 3);
        double wave01 = 0.5 + 0.5 * Math.Sin(glowPhase);
        // Premium constant chip: deep indigo → deep cyan gradient with a white
        // microphone glyph; the state lives in the caption color and the pulse
        // rings, never in the chip background.
        using (GraphicsPath path = RoundedPath(chip, 12))
        {
            using (var fill = new LinearGradientBrush(chip,
                Color.FromArgb(255, 91, 75, 232),
                Color.FromArgb(255, 14, 134, 180), 45f))
                e.Graphics.FillPath(fill, path);
            using (var glow = new Pen(Color.FromArgb((int)(40 + 70 * glowStrength * (0.4 + 0.6 * wave01)), 124, 100, 255), 1.6f))
                e.Graphics.DrawPath(glow, path);
        }
        if (control.Text.Length == 0)
        {
            DrawVectorMicrophone(e.Graphics, control.Width, control.Height);
        }
        // State light-wave: two expanding pulse rings around the microphone.
        // Purely a state indicator; it never represents audio data.
        for (int ring = 0; ring < 2; ring++)
        {
            double t = ((glowPhase / 6.2831f) + ring * 0.5) % 1.0;
            int offset = 2 + (int)(t * 13);
            int alpha = (int)((1.0 - t) * 140 * glowStrength);
            if (alpha <= 2) continue;
            Color ringColor = ring == 0
                ? Color.FromArgb(alpha, 124, 100, 255)
                : Color.FromArgb(alpha, 0, 168, 222);
            Rectangle ringRect = Rectangle.FromLTRB(
                chip.Left - offset, chip.Top - offset, chip.Right + offset, chip.Bottom + offset);
            using (var pen = new Pen(ringColor, 2f))
            using (GraphicsPath ringPath = RoundedPath(ringRect, 12 + offset))
                e.Graphics.DrawPath(pen, ringPath);
        }
    }

    private void DrawVectorMicrophone(Graphics g, int width, int height)
    {
        int cx = width / 2;
        int cy = height / 2;
        using (var white = new SolidBrush(Color.White))
        using (var capsule = RoundedPath(new Rectangle(cx - 5, cy - 14, 10, 22), 5))
        {
            g.FillPath(white, capsule);
        }
        using (var pen = new Pen(Color.White, 2f))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            g.DrawLine(pen, cx - 6, cy + 8, cx - 6, cy + 13);
            g.DrawLine(pen, cx + 6, cy + 8, cx + 6, cy + 13);
            g.DrawArc(pen, cx - 6, cy + 7, 12, 12, 180f, 180f);
            g.DrawLine(pen, cx - 10, cy + 22, cx + 10, cy + 22);
        }
    }

    private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
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

    internal void ApplySnapshot(VibeUiStatusSnapshot snapshot)
    {
        if (snapshot == null || IsDisposed) return;
        ActionResult action = snapshot.LatestAction;
        bool processing = !snapshot.RealAudioActive &&
            string.Equals(snapshot.VoiceStatus, "录音已结束，等待语音工具处理",
                StringComparison.Ordinal);
        ActionState displayState = snapshot.RealAudioActive ? ActionState.Running :
            processing ? ActionState.Checking : action.State;
        accent = StateColor(displayState);
        audioRmsPercent = snapshot.AudioRmsPercent;
        stateGlyph.Text = StateGlyph(displayState);
        stateGlyph.ForeColor = Color.White;
        ApplyGlowForState(displayState);
        titleLabel.Text = snapshot.RealAudioActive ? "正在接收真实音频" :
            processing ? snapshot.VoiceStatus :
            action.State == ActionState.Idle ? snapshot.VoiceStatus : action.Stage;
        detailLabel.Text = snapshot.RealAudioActive ?
            "录音键按住期间持续接收；松开后等待语音工具处理" :
            processing ? "音频已交给语音工具 · 最终文字请目视确认" :
            action.State == ActionState.Idle ? snapshot.DeviceStatus + " · 等待操作" :
            string.IsNullOrWhiteSpace(action.OverlayDetailText()) ? snapshot.VoiceStatus : action.OverlayDetailText();
        // Name the voice tool beside the workflow target. The title and detail above already carry the session state, so
        // this line answers "which tool, aiming at what" while they answer "what is happening right now".
        contextLabel.Text = "工具 · " + (string.IsNullOrWhiteSpace(snapshot.VoiceToolName) ? "未选择" : snapshot.VoiceToolName) +
            "  ｜  目标 · " + (string.IsNullOrWhiteSpace(snapshot.FocusTargetName) ? "未设置" : snapshot.FocusTargetName);
        RefreshMeter(snapshot.RealAudioActive);
        ApplyGlyphRegion();
        Invalidate(true);
    }

    private void ApplyGlowForState(ActionState state)
    {
        // State light-wave profile. Recording pulses fastest and strongest;
        // processing is calm cyan; errors pulse noticeably but slower; success
        // is a soft green shimmer; idle stays subtle.
        switch (state)
        {
            case ActionState.Running: glowSpeed = 0.24f; glowStrength = 1.0f; break;
            case ActionState.Checking: glowSpeed = 0.13f; glowStrength = 0.8f; break;
            case ActionState.Error: glowSpeed = 0.18f; glowStrength = 0.95f; break;
            case ActionState.Warning:
            case ActionState.Canceled: glowSpeed = 0.11f; glowStrength = 0.7f; break;
            case ActionState.Success: glowSpeed = 0.06f; glowStrength = 0.5f; break;
            default: glowSpeed = 0.05f; glowStrength = 0.35f; break;
        }
    }

    private void RefreshMeter(bool realAudioActive)
    {
        // The meter only ever renders measured audio. While audio is arriving
        // there is no live RMS stream from the frozen capture, so the bars stay
        // dim and the caption stays honest; the bars light up from the session's
        // real measured peak once the stream stop reports output_rms_pct.
        meter.SetLevel(audioRmsPercent);
        if (realAudioActive)
        {
            meterCaption.Text = "真实音频正在到达 · 峰值松开录音键后回传";
            meterCaption.ForeColor = accent;
        }
        else if (audioRmsPercent > 0)
        {
            meterCaption.Text = "上次真实音频峰值 " + audioRmsPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            meterCaption.ForeColor = accent;
        }
        else
        {
            meterCaption.Text = "真实音频峰值 · 等待一次真实听写";
            meterCaption.ForeColor = MutedColor();
        }
        meter.Invalidate();
    }

    private bool darkTheme;

    internal void ApplyTheme(bool useDarkTheme)
    {
        if (IsDisposed) return;
        darkTheme = useDarkTheme;
        // The HUD floats above every page, so it keeps one refined dark-glass
        // surface in both themes; only the glass depth and outline adjust.
        Color background = useDarkTheme ? Color.FromArgb(16, 19, 31) : Color.FromArgb(20, 23, 38);
        Color outline = useDarkTheme ? Color.FromArgb(45, 48, 68) : Color.FromArgb(52, 55, 78);
        Color ink = Color.FromArgb(232, 236, 246);
        Color body = Color.FromArgb(176, 184, 202);
        BackColor = background;
        border.BackColor = outline;
        surface.BackColor = background;
        titleLabel.ForeColor = ink;
        detailLabel.ForeColor = body;
        contextLabel.ForeColor = MutedColor();
        closeLabel.ForeColor = MutedColor();
        stateGlyph.Invalidate();
        RefreshMeter(false);
        Invalidate(true);
    }

    private Color MutedColor()
    {
        return darkTheme ? Color.FromArgb(158, 166, 186) : Color.FromArgb(168, 176, 196);
    }

    internal void PositionOnWorkingArea(Screen screen)
    {
        Rectangle area = (screen ?? Screen.PrimaryScreen).WorkingArea;
        Location = new Point(area.Right - Width - 18, area.Bottom - Height - 18);
    }

    internal bool IsPresented
    {
        get { return IsHandleCreated && IsWindowVisible(Handle); }
    }

    internal void PrepareInactiveHandle()
    {
        if (IsDisposed || IsHandleCreated) return;
        IntPtr preparedHandle = Handle;
        PrepareNativeControlTree(this);
        ShowWindow(preparedHandle, 0);
    }

    private static void PrepareNativeControlTree(Control root)
    {
        foreach (Control child in root.Controls)
        {
            IntPtr preparedChildHandle = child.Handle;
            PrepareNativeControlTree(child);
        }
    }

    internal void ShowInactive(Form owner)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (owner != null && Owner != owner) Owner = owner;
        PositionOnWorkingArea(owner == null ? Screen.FromPoint(Cursor.Position) : Screen.FromControl(owner));
        IntPtr windowHandle = Handle;
        ShowWindow(windowHandle, SW_SHOWNOACTIVATE);
        SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        glowTimer.Start();
    }

    internal void HideInactive()
    {
        if (IsHandleCreated) ShowWindow(Handle, 0);
        glowTimer.Stop();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyFormRegion();
        PositionOnWorkingArea(Screen.FromPoint(Cursor.Position));
        SetWindowPos(Handle, HWND_TOPMOST, Left, Top, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            glowTimer.Stop();
            glowTimer.Dispose();
            titleFont.Dispose();
            detailFont.Dispose();
            glyphFont.Dispose();
            captionFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Color StateColor(ActionState state)
    {
        // Shared with the in-window toast so the two surfaces can never disagree.
        return UiDesignTokens.StatusAccent(state);
    }

    private static string StateGlyph(ActionState state)
    {
        // Empty means the running/checking/idle states draw the vector microphone below,
        // so the icon stays crisp and visible on any system font.
        return UiDesignTokens.StatusGlyph(state);
    }

    // Static meter bars. There is no timer and no sine/pulse animation inside
    // the HUD: each snapshot repaints the last measured level verbatim.
    private sealed class RmsMeterPanel : Panel
    {
        private double level;
        public RmsMeterPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        public void SetLevel(double value) { level = Math.Max(0.0, Math.Min(100.0, value)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int lit = (int)Math.Round(level / 20.0);
            int gap = 4;
            int barWidth = Math.Max(4, (Width - gap * 4) / 5);
            for (int i = 0; i < 5; i++)
            {
                var bar = new Rectangle(i * (barWidth + gap), 1, barWidth, Math.Max(4, Height - 2));
                Color color = i < lit
                    ? Color.FromArgb(255, 104 - i * 16, 82 + i * 14, 244 - i * 34)
                    : Color.FromArgb(40, 172, 182, 255);
                using (var brush = new SolidBrush(color))
                using (GraphicsPath path = RoundedPath(new Rectangle(bar.X, bar.Y, bar.Width, bar.Height), 3))
                    e.Graphics.FillPath(brush, path);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y,
        int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}

internal sealed partial class VibeMicForm
{
    private LiveHudForm liveHud;
    private System.Windows.Forms.Timer liveHudHideTimer;
    private bool liveHudSessionSuppressed;
    // True only while the user has asked for the HUD from the tray. An explicit request is
    // an addition to the in-window card, never a replacement for it, and an action toast
    // must not take the HUD away from a user who asked for it.
    private bool liveHudExplicitlyRequested;
    private string liveHudPresentationKey = "";
    private DateTime liveHudAutoHideAtUtc = DateTime.MinValue;
    private ContextDeckForm contextDeck;
    private VibeUiStatusSnapshot latestUiSnapshot;
    private ActionResult latestActionResult = ActionResult.Create("状态", "Vibe Flow", ActionState.Idle,
        "等待操作", "", "", "");
    private int latestDeckBridgeProcessId;
    private long latestDeckExecutionSequence;
    private string latestHighlightedControl = "";
    private void ShowLiveHud()
    {
        if (applicationExiting || IsDisposed) return;
        liveHudSessionSuppressed = false;
        liveHudExplicitlyRequested = true;
        latestUiSnapshot = BuildFeedbackSnapshot();
        PresentLiveHud(latestUiSnapshot, true);
    }

    private void EnsureLiveHud()
    {
        if (liveHud == null || liveHud.IsDisposed)
        {
            liveHud = new LiveHudForm();
            liveHud.ApplyTheme(darkTheme);
            liveHud.Dismissed += OnLiveHudDismissed;
            liveHud.PrepareInactiveHandle();
        }
        if (liveHudHideTimer == null)
        {
            liveHudHideTimer = new System.Windows.Forms.Timer();
            liveHudHideTimer.Interval = 250;
            liveHudHideTimer.Tick += OnLiveHudHideTimerTick;
        }
    }

    private void OnLiveHudDismissed(object sender, EventArgs e)
    {
        liveHudSessionSuppressed = true;
        // The user closed it, so it is no longer an explicit request either; later actions
        // go back to choosing their own single outlet.
        liveHudExplicitlyRequested = false;
        liveHudAutoHideAtUtc = DateTime.MinValue;
        if (liveHudHideTimer != null) liveHudHideTimer.Stop();
    }

    private void OnLiveHudHideTimerTick(object sender, EventArgs e)
    {
        if (liveHudAutoHideAtUtc == DateTime.MinValue) return;
        int remaining = (int)Math.Ceiling((liveHudAutoHideAtUtc - DateTime.UtcNow).TotalMilliseconds);
        if (remaining > 0)
        {
            liveHudHideTimer.Interval = Math.Max(50, Math.Min(1000, remaining));
            return;
        }
        liveHudHideTimer.Stop();
        liveHudAutoHideAtUtc = DateTime.MinValue;
        if (liveHud != null && !liveHud.IsDisposed) liveHud.HideInactive();
    }

    private void PresentLiveHud(VibeUiStatusSnapshot snapshot, bool force)
    {
        PresentLiveHud(snapshot, force, false);
    }

    // carryMessage=true means this presentation carries a transient action message rather than
    // live state, so its lifetime is bounded even for the running/checking states.
    private void PresentLiveHud(VibeUiStatusSnapshot snapshot, bool force, bool carryMessage)
    {
        if (snapshot == null || applicationExiting || IsDisposed) return;
        string key = LiveHudKey(snapshot);
        bool firstSnapshot = string.IsNullOrWhiteSpace(liveHudPresentationKey);
        bool changed = !string.Equals(key, liveHudPresentationKey, StringComparison.Ordinal);
        liveHudPresentationKey = key;
        bool initialIdleBaseline = firstSnapshot && !snapshot.RealAudioActive &&
            snapshot.LatestAction.State == ActionState.Idle;
        if (!force && (initialIdleBaseline || !changed || liveHudSessionSuppressed))
        {
            if (liveHud != null && !liveHud.IsDisposed) liveHud.ApplySnapshot(snapshot);
            return;
        }
        EnsureLiveHud();
        liveHud.ApplySnapshot(snapshot);
        liveHud.ApplyTheme(darkTheme);
        liveHud.ShowInactive(null);

        int duration = carryMessage
            ? FeedbackMessageLifetimeMilliseconds(snapshot.RealAudioActive, snapshot.LatestAction.State)
            : LiveHudDurationMilliseconds(snapshot);
        liveHudHideTimer.Stop();
        if (duration <= 0)
        {
            liveHudAutoHideAtUtc = DateTime.MinValue;
            return;
        }
        liveHudAutoHideAtUtc = DateTime.UtcNow.AddMilliseconds(duration);
        liveHudHideTimer.Interval = Math.Max(50, Math.Min(1000, duration));
        liveHudHideTimer.Start();
    }

    private static string LiveHudKey(VibeUiStatusSnapshot snapshot)
    {
        ActionResult action = snapshot == null ? null : snapshot.LatestAction;
        return (snapshot == null ? "" : snapshot.RealAudioActive ? "audio:1" : "audio:0") + "|" +
            (snapshot == null ? "" : snapshot.DeviceStatus) + "|" +
            (snapshot == null ? "" : snapshot.VoiceStatus) + "|" +
            (action == null ? "" : action.TimestampUtc.Ticks.ToString()) + "|" +
            (action == null ? "" : action.State.ToString()) + "|" +
            (action == null ? "" : action.Message);
    }

    // How long the floating panel stays up when it is presenting live STATE. An operation that
    // is still in flight must not auto-hide, and neither must live audio.
    private static int LiveHudDurationMilliseconds(VibeUiStatusSnapshot snapshot)
    {
        if (snapshot == null || snapshot.RealAudioActive) return 0;
        ActionState state = snapshot.LatestAction.State;
        if (state == ActionState.Running || state == ActionState.Checking) return 0;
        return UiDesignTokens.DurationForState(state);
    }

    // How long it stays up when it is carrying a transient MESSAGE (an action toast). This is
    // deliberately always bounded: ActionResult.FromLegacyFeedback classifies any message whose
    // text begins with 「正在」 as Checking, so reusing the state rule here let an ordinary
    // informational toast ("正在检查蓝牙和遥控器语音通道") pin the always-on-top panel in the
    // bottom-right corner indefinitely — which is what made it read as a permanent dock.
    internal static int FeedbackMessageLifetimeMilliseconds(bool realAudioActive, ActionState state)
    {
        if (realAudioActive) return 0;   // live audio still pins it open
        if (state == ActionState.Running || state == ActionState.Checking)
            return UiDesignTokens.FeedbackProblemDurationMs;
        return UiDesignTokens.DurationForState(state);
    }

    private void RefreshLiveHudTheme()
    {
        if (liveHud != null && !liveHud.IsDisposed) liveHud.ApplyTheme(darkTheme);
    }

    private void RefreshContextDeckTheme()
    {
        if (contextDeck != null && !contextDeck.IsDisposed) contextDeck.ApplyTheme(darkTheme);
    }

    private void ShowContextDeck()
    {
        if (applicationExiting || IsDisposed) return;
        if (BlockContextDeckOpeningForRecording()) return;
        if (contextDeck == null || contextDeck.IsDisposed)
        {
            contextDeck = new ContextDeckForm();
            contextDeck.FormClosed += delegate { contextDeck = null; };
        }
        contextDeck.ApplyTheme(darkTheme);
        PublishFeedbackSnapshot();
        contextDeck.ApplySnapshot(latestUiSnapshot);
        if (BlockContextDeckOpeningForRecording()) return;
        if (!contextDeck.Visible) contextDeck.ShowInactive(this);
        else contextDeck.BringToFront();
        BlockContextDeckOpeningForRecording();
    }

    private bool BlockContextDeckOpeningForRecording()
    {
        if (!ContextDeckOpeningBlockedByRecording(IsVoiceKeyHeld(), currentVisualState)) return false;
        if (contextDeck != null && !contextDeck.IsDisposed && contextDeck.Visible)
        {
            try { contextDeck.HideInactive(); } catch { }
        }
        latestActionResult = ActionResult.Create("打开遥控器状态", "当前遥控器", ActionState.Warning,
            "录音键正在使用，Deck 未打开", "录音操作优先", "松开录音键后重试", "DECK-RECORDING");
        PublishFeedbackSnapshot();
        return true;
    }

    private void HideContextDeckForRecording()
    {
        if (applicationExiting || IsDisposed) return;
        if (InvokeRequired)
        {
            DispatchUi(HideContextDeckForRecording);
            return;
        }
        if (contextDeck == null || contextDeck.IsDisposed || !contextDeck.Visible) return;
        try { contextDeck.HideInactive(); } catch { }
    }

    private static bool ContextDeckOpeningBlockedByRecording(bool voiceKeyHeld, string visualState)
    {
        return voiceKeyHeld || string.Equals(visualState, "recording", StringComparison.OrdinalIgnoreCase);
    }

    private void PublishFeedbackSnapshot()
    {
        PublishFeedbackSnapshotInternal(true, false);
    }

    // presentHud=false keeps the HUD's content current without showing it: the caller has
    // chosen the in-window card as this message's single outlet. carryMessage=true says the
    // panel is carrying a transient action message, so its lifetime must stay bounded.
    private void PublishFeedbackSnapshotInternal(bool presentHud, bool carryMessage)
    {
        if (applicationExiting || IsDisposed) return;
        if (InvokeRequired)
        {
            DispatchUi(delegate { PublishFeedbackSnapshotInternal(presentHud, carryMessage); });
            return;
        }
        latestUiSnapshot = BuildFeedbackSnapshot();
        if (contextDeck != null && !contextDeck.IsDisposed)
        {
            contextDeck.ApplySnapshot(latestUiSnapshot);
            if (latestUiSnapshot.RealAudioActive && contextDeck.Visible) contextDeck.HideInactive();
        }
        if (presentHud) PresentLiveHud(latestUiSnapshot, false, carryMessage);
        else if (liveHud != null && !liveHud.IsDisposed) liveHud.ApplySnapshot(latestUiSnapshot);
    }

    private VibeUiStatusSnapshot BuildFeedbackSnapshot()
    {
        BridgeHealthSnapshot bridge = ReadKeyboardBridgeHealth();
        UpdateLatestBridgeAction(bridge);
        ShortcutProfileConfig activeProfile = ActiveShortcutProfile(config);
        string profileName = bridge != null && !string.IsNullOrWhiteSpace(bridge.SmartEffectiveProfileName)
            ? bridge.SmartEffectiveProfileName : activeProfile == null ? "通用导航" : activeProfile.name;
        string deviceStatus = bridgeReady ? "设备已连接" : IsCapturing ? "设备正在连接" : "设备未连接";
        string voiceStatus = currentVisualState == "recording" ? "正在接收真实音频" :
            currentVisualState == "processing" ? "录音已结束，等待语音工具处理" :
            currentVisualState == "error" ? "语音链路需要检查" :
            bridgeReady ? "语音桥接就绪" : IsCapturing ? "语音桥接正在连接" : "语音桥接已暂停";
        string highlighted = DateTime.Now < remoteHighlightUntil ? latestHighlightedControl : "";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        mappings["录音键"] = "按住开始 · 松开结束";
        mappings["上键"] = MappingCardActionText(GetMapping("上键", "up"));
        mappings["下键"] = MappingCardActionText(GetMapping("下键", "down"));
        mappings["左键"] = MappingCardActionText(GetMapping("左键", "left"));
        mappings["右键"] = MappingCardActionText(GetMapping("右键", "right"));
        mappings["确认键"] = MappingCardActionText(GetMapping("确认键", "enter"));
        mappings["Home 短按"] = MappingCardActionText(GetMapping("Home:short", "win+d"));
        mappings["Home 长按"] = MappingCardActionText(GetMapping("Home:long", "none"));
        mappings["TV"] = MappingCardActionText(GetMapping("TV", "task-switcher"));
        mappings["功能键短按"] = MappingCardActionText(GetMapping("功能键:short", "ctrl+c"));
        mappings["功能键长按"] = MappingCardActionText(GetMapping("功能键:long", "ctrl+v"));
        return VibeUiStatusSnapshot.Create(deviceStatus, voiceStatus, profileName,
            bridge == null ? "" : bridge.SmartForegroundProcess, FocusTargetSummary(),
            string.IsNullOrWhiteSpace(currentProjectSpaceName) ? "未配置项目" : currentProjectSpaceName, highlighted,
            currentVisualState == "recording", latestAudioOutputRmsPercent, latestActionResult, mappings,
            ProviderDisplayName(config.inputMethod));
    }

    private void UpdateLatestBridgeAction(BridgeHealthSnapshot bridge)
    {
        bool fresh = bridge != null && bridge.FileAgeSeconds <= 7;
        if (bridge == null || !ShouldAcceptBridgeExecution(latestDeckBridgeProcessId,
            latestDeckExecutionSequence, bridge.ProcessId, bridge.LastExecutionSequence, fresh)) return;
        if (bridge.ProcessId > 0) latestDeckBridgeProcessId = bridge.ProcessId;
        latestDeckExecutionSequence = bridge.LastExecutionSequence;
        string label = string.IsNullOrWhiteSpace(bridge.LastExecutionLabel)
            ? bridge.LastExecutionButton : bridge.LastExecutionLabel;
        string trigger = string.IsNullOrWhiteSpace(bridge.LastExecutionTrigger) ? "单击" : bridge.LastExecutionTrigger;
        string action = OverlayText.SanitizeMappingAction(bridge.LastExecutionAction);
        string normalized = (bridge.LastExecutionAction ?? "").Trim().ToLowerInvariant();
        bool disabled = normalized.Length == 0 || normalized == "none" || normalized == "passthrough";
        latestHighlightedControl = RemoteControlForBridgeButton(bridge.LastExecutionButton);
        if (!string.IsNullOrEmpty(latestHighlightedControl)) remoteHighlightUntil = DateTime.Now.AddMilliseconds(520);
        latestActionResult = ActionResult.Create(label + " · " + trigger,
            string.IsNullOrWhiteSpace(bridge.LastExecutionProfileName) ? "当前 Profile" : bridge.LastExecutionProfileName,
            disabled ? ActionState.Warning : bridge.LastExecutionSuccess ? ActionState.Success : ActionState.Error,
            disabled ? "未配置动作" : action + (bridge.LastExecutionSuccess ? " · 已执行" : " · 执行失败"),
            bridge.LastExecutionSuccess || disabled ? "" : "按键服务未确认动作完成",
            bridge.LastExecutionSuccess || disabled ? "" : "打开自检查看原因",
            bridge.LastExecutionSuccess || disabled ? "" : "BRIDGE-ACTION-FAILED");
    }

    private static bool ShouldAcceptBridgeExecution(int previousProcessId, long previousSequence,
        int currentProcessId, long currentSequence, bool healthFresh)
    {
        if (!healthFresh || currentSequence <= 0) return false;
        bool identifiedRestart = currentProcessId > 0 && previousProcessId != currentProcessId;
        return identifiedRestart || currentSequence > previousSequence;
    }

    private static string RemoteControlForBridgeButton(string button)
    {
        string normalized = (button ?? "").Trim().ToLowerInvariant();
        if (normalized.IndexOf("录音") >= 0 || normalized == "voice") return "voice";
        if (normalized.IndexOf("确认") >= 0 || normalized == "ok") return "ok";
        if (normalized.IndexOf("home") >= 0) return "home";
        if (normalized.IndexOf("tv") >= 0) return "tv";
        if (normalized.IndexOf("功能") >= 0 || normalized == "menu") return "menu";
        if (normalized.IndexOf("上") >= 0 || normalized == "up") return "up";
        if (normalized.IndexOf("下") >= 0 || normalized == "down") return "down";
        if (normalized.IndexOf("左") >= 0 || normalized == "left") return "left";
        if (normalized.IndexOf("右") >= 0 || normalized == "right") return "right";
        return "";
    }

    private void DisposeFeedbackSurfaces()
    {
        if (liveHudHideTimer != null)
        {
            liveHudHideTimer.Stop();
            liveHudHideTimer.Tick -= OnLiveHudHideTimerTick;
            liveHudHideTimer.Dispose();
            liveHudHideTimer = null;
        }
        if (liveHud != null)
        {
            liveHud.Dismissed -= OnLiveHudDismissed;
            try { liveHud.HideInactive(); } catch { }
            try { liveHud.Dispose(); } catch { }
            liveHud = null;
        }
        if (contextDeck != null)
        {
            try { contextDeck.Dispose(); } catch { }
            contextDeck = null;
        }
        latestUiSnapshot = null;
    }
}
