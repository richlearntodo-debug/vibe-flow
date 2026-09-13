using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The redesigned Smart Focus entry point. It lists both what is running right now and what
// is merely installed, because a favourite is allowed to be an application the user has not
// started yet: picking it launches it and then learns its input box.
//
// The only text field in the whole feature lives here and it is a filter: it narrows the
// machine's own list, it never becomes user content and it is never stored.
//
// Every row carries an icon. Measured on this machine, many catalogue entries arrive with no icon at all — an
// application whose executable has no icon resource, a packaged application resolved through its AppUserModelID,
// a shortcut the shell draws an image for but the extractor cannot — and a list where some rows have a picture
// and others have a blank gap reads as broken. The lookup order below ends in a generated tile, so there is no
// empty slot; the tile is drawn from the name, which is also what makes it stable between openings.
internal sealed class AppPickerDialog : Form
{
    private readonly ListBox applications = new ListBox();
    private readonly TextBox filter = new TextBox();
    private readonly Label filterPlaceholder = new Label();
    private readonly Panel searchFrame = new Panel();
    private readonly Panel listFrame = new Panel();
    private readonly Label hint = new Label();
    private readonly Label emptyState = new Label();
    private readonly List<InstalledAppChoice> allChoices = new List<InstalledAppChoice>();
    private Label titleLabel;
    private Label subtitleLabel;
    private Button confirmButton;
    private Button cancelButton;
    private readonly Action<string> logger;
    private int iconFromCatalogue;
    private int iconFromProcess;
    private int iconFromTarget;
    private int iconFromTile;
    private bool rebuilding;
    private int hoverIndex = -1;

    // The palette mirrors the host's (violet 104,82,244 and the light surfaces) so the dialog belongs to the
    // application instead of looking like a system dialog that happened to be called from it, and it follows the
    // night theme, which this dialog did not do before.
    private bool darkTheme;
    private Color pageColor;
    private Color cardColor;
    private Color inkColor;
    private Color mutedColor;
    private Color lineColor;
    private Color accentColor;
    private Color rowSelected;
    private Color rowHover;

    internal string SelectedProcessName { get; private set; }
    internal string SelectedLaunchTarget { get; private set; }
    internal string SelectedLaunchArguments { get; private set; }

    internal AppPickerDialog(IList<InstalledAppChoice> choices)
        : this(choices, false, null)
    {
    }

    internal AppPickerDialog(IList<InstalledAppChoice> choices, bool darkTheme)
        : this(choices, darkTheme, null)
    {
    }

    internal AppPickerDialog(IList<InstalledAppChoice> choices, bool darkTheme, Action<string> log)
    {
        logger = log;
        // Its layout is built at 96 dpi at runtime, so it is scaled onto the display it opens on: measured
        // at 200%, this dialog drew its title with a doubled font inside a 1x box and cut its subtitle off.
        UiDisplayScale.Apply(this);
        SelectedProcessName = "";
        SelectedLaunchTarget = "";
        SelectedLaunchArguments = "";
        Text = "添加应用";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 640);
        Size = new Size(600, 700);
        Font = new Font("Microsoft YaHei UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ApplyTheme(darkTheme);

        titleLabel = new Label
        {
            Text = "添加应用",
            Location = new Point(24, 20),
            Size = new Size(300, 30),
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        subtitleLabel = new Label
        {
            Text = "从本机已安装的应用里选一个；正在运行的排在前面，未运行的选中后会自动打开。",
            Location = new Point(24, 52),
            Size = new Size(552, 24),
            Font = new Font("Microsoft YaHei UI", 8.8f),
            BackColor = Color.Transparent
        };
        Controls.Add(subtitleLabel);

        // The search field: a borderless text box inside a rounded frame, with a glyph and a placeholder that
        // disappears as soon as there is text. Filtering is live and takes no step of its own.
        searchFrame.Location = new Point(24, 86);
        searchFrame.Size = new Size(552, 40);
        searchFrame.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = AppIcons.RoundedPath(new Rectangle(0, 0, searchFrame.Width - 1, searchFrame.Height - 1), 10))
            using (var fill = new SolidBrush(cardColor))
            using (var pen = new Pen(lineColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
            using (var glyphFont = new Font("Segoe MDL2 Assets", 11f))
            using (var glyphBrush = new SolidBrush(mutedColor))
                e.Graphics.DrawString("\uE721", glyphFont, glyphBrush, 12, 11);
        };
        Controls.Add(searchFrame);

        filter.Location = new Point(40, 10);
        filter.Size = new Size(498, 22);
        filter.BorderStyle = BorderStyle.None;
        filter.BackColor = cardColor;
        filter.ForeColor = inkColor;
        filter.TextChanged += delegate
        {
            filterPlaceholder.Visible = filter.Text.Length == 0;
            ApplyFilter();
        };
        searchFrame.Controls.Add(filter);
        filterPlaceholder.Text = "搜索应用名或进程名，例如 cursor / chrome";
        filterPlaceholder.Location = new Point(42, 11);
        filterPlaceholder.Size = new Size(470, 20);
        filterPlaceholder.Font = new Font("Microsoft YaHei UI", 9.2f);
        filterPlaceholder.BackColor = Color.Transparent;
        filterPlaceholder.ForeColor = mutedColor;
        filterPlaceholder.Cursor = Cursors.IBeam;
        filterPlaceholder.Click += delegate { filter.Focus(); };
        searchFrame.Controls.Add(filterPlaceholder);
        // The text box is created first and paints an opaque background, so the placeholder has to be brought in
        // front of it or it is never seen at all.
        filterPlaceholder.BringToFront();

        listFrame.Location = new Point(24, 138);
        listFrame.Size = new Size(552, 448);
        listFrame.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = AppIcons.RoundedPath(new Rectangle(0, 0, listFrame.Width - 1, listFrame.Height - 1), 10))
            using (var fill = new SolidBrush(cardColor))
            using (var pen = new Pen(lineColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        };
        Controls.Add(listFrame);

        applications.Location = new Point(2, 2);
        applications.Size = new Size(548, 444);
        applications.BorderStyle = BorderStyle.None;
        applications.BackColor = cardColor;
        applications.IntegralHeight = false;
        applications.DrawMode = DrawMode.OwnerDrawFixed;
                // 44 rather than 54: with 95 applications in the list, eight rows on screen meant a lot of scrolling. The
        // row's contents are laid out from this height, so the icon and the two text lines follow it.
        applications.ItemHeight = 44;
        applications.DrawItem += DrawRow;
        applications.MouseMove += delegate(object sender, MouseEventArgs e)
        {
            int index = applications.IndexFromPoint(e.Location);
            if (index != hoverIndex)
            {
                hoverIndex = index;
                applications.Invalidate();
            }
        };
        applications.MouseLeave += delegate
        {
            if (hoverIndex != -1)
            {
                hoverIndex = -1;
                applications.Invalidate();
            }
        };
        applications.DoubleClick += delegate { ConfirmSelection(); };
        listFrame.Controls.Add(applications);

        // Drawn over the list rather than substituted for it, so the geometry never changes when a filter
        // matches nothing.
        emptyState.Text = "没有匹配的应用。换个词试试，或清掉筛选看全部。";
        emptyState.Location = new Point(2, 190);
        emptyState.Size = new Size(548, 26);
        emptyState.TextAlign = ContentAlignment.MiddleCenter;
        emptyState.Font = new Font("Microsoft YaHei UI", 9.2f);
        emptyState.BackColor = Color.Transparent;
        emptyState.Visible = false;
        listFrame.Controls.Add(emptyState);

        hint.Location = new Point(24, 600);
        hint.Size = new Size(320, 22);
        hint.Font = new Font("Microsoft YaHei UI", 8.8f);
        hint.BackColor = Color.Transparent;
        Controls.Add(hint);

        confirmButton = MakeButton("确定", new Point(352, 594), true);
        confirmButton.Click += delegate { ConfirmSelection(); };
        Controls.Add(confirmButton);

        cancelButton = MakeButton("取消", new Point(472, 594), false);
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;

        if (choices != null)
        {
            foreach (InstalledAppChoice choice in choices)
            {
                if (choice != null && !string.IsNullOrWhiteSpace(choice.ProcessName)) allChoices.Add(choice);
            }
        }
        // Applied a second time now that every control exists. The first call established the palette the controls
        // are created with; this one styles the buttons and frames, which did not exist yet when it ran — measured,
        // the confirm button rendered as an unstyled system button because StyleButton ran while its field was null.
        ApplyTheme(darkTheme);
        // Every icon is resolved once here rather than during the first paint of each row: the counts below are the
        // measurement of how many applications still fall through to a generated tile, and a per-paint lookup also
        // made the first paint of every row do shell work.
        var started = DateTime.UtcNow;
        foreach (InstalledAppChoice choice in allChoices) IconFor(choice);
        if (logger != null)
        {
            logger("PICKER ICONS total=" + allChoices.Count + " catalogue=" + iconFromCatalogue +
                " process=" + iconFromProcess + " target=" + iconFromTarget + " tile=" + iconFromTile +
                " elapsedMs=" + (int)(DateTime.UtcNow - started).TotalMilliseconds);
        }
        ApplyFilter();
        filter.Focus();
    }

    // The host paints with its own palette; this dialog takes the same two sets of values so it does not stay
    // white inside a night-themed application.
    internal void ApplyTheme(bool dark)
    {
        darkTheme = dark;
        pageColor = dark ? Color.FromArgb(25, 26, 31) : Color.FromArgb(247, 249, 252);
        cardColor = dark ? Color.FromArgb(35, 37, 44) : Color.White;
        inkColor = dark ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
        mutedColor = dark ? Color.FromArgb(153, 161, 177) : Color.FromArgb(112, 120, 138);
        lineColor = dark ? Color.FromArgb(55, 59, 69) : Color.FromArgb(220, 226, 239);
        accentColor = dark ? Color.FromArgb(150, 135, 255) : Color.FromArgb(104, 82, 244);
        rowSelected = dark ? Color.FromArgb(52, 47, 88) : Color.FromArgb(238, 235, 255);
        rowHover = dark ? Color.FromArgb(42, 44, 52) : Color.FromArgb(246, 247, 252);
        BackColor = pageColor;
        ForeColor = inkColor;
        applications.BackColor = cardColor;
        applications.ForeColor = inkColor;
        filter.BackColor = cardColor;
        filter.ForeColor = inkColor;
        if (filterPlaceholder != null) filterPlaceholder.ForeColor = mutedColor;
        if (hint != null) hint.ForeColor = mutedColor;
        if (emptyState != null) emptyState.ForeColor = mutedColor;
        if (titleLabel != null) titleLabel.ForeColor = inkColor;
        if (subtitleLabel != null) subtitleLabel.ForeColor = mutedColor;
        StyleButton(confirmButton, true);
        StyleButton(cancelButton, false);
        Invalidate(true);
        if (searchFrame != null) searchFrame.Invalidate();
        if (listFrame != null) listFrame.Invalidate();
    }

    private void StyleButton(Button button, bool primary)
    {
        if (button == null) return;
        button.BackColor = primary ? accentColor : cardColor;
        button.ForeColor = primary ? Color.White : inkColor;
        button.FlatAppearance.BorderColor = primary ? accentColor : lineColor;
        button.FlatAppearance.MouseOverBackColor = primary
            ? ControlPaint.Light(accentColor, 0.12f) : rowHover;
    }

    private Button MakeButton(string text, Point location, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Location = location,
            Size = new Size(104, 40),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9.4f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = primary ? DialogResult.None : DialogResult.Cancel
        };
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    // One row: icon tile, name, and what the application is doing right now. Every measurement is taken from the
    // row height instead of being a fixed pixel offset, because the row height is scaled for the display while a
    // constant is not — at 200% the earlier fixed offsets left the icon and the two text lines bunched at the top
    // of a doubled row.
    private void DrawRow(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= applications.Items.Count) return;
        var item = applications.Items[e.Index] as InstalledAppChoice;
        if (item == null) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        int rowHeight = e.Bounds.Height;
        int iconSize = Math.Max(20, rowHeight - 16);
        int iconLeft = e.Bounds.Left + Math.Max(8, rowHeight / 6);
        int iconTop = e.Bounds.Top + (rowHeight - iconSize) / 2;
        int textLeft = iconLeft + iconSize + Math.Max(10, rowHeight / 5);

        using (var background = new SolidBrush(selected ? rowSelected : e.Index == hoverIndex && !selected
            ? rowHover : cardColor))
            e.Graphics.FillRectangle(background, e.Bounds);
        if (selected)
        {
            using (var bar = new SolidBrush(accentColor))
                e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, Math.Max(3, rowHeight / 16), rowHeight);
        }

        Image icon = IconFor(item);
        if (icon != null)
        {
            // On a subtle tile rather than straight onto the card: several application icons are drawn for a white
            // or a dark background and carry transparency, so on the card they are nearly invisible — measured, that
            // is what many of the "missing" logos actually were.
            AppIcons.DrawTile(e.Graphics, icon, new Rectangle(iconLeft, iconTop, iconSize, iconSize),
                darkTheme ? Color.FromArgb(45, 47, 56) : Color.FromArgb(243, 245, 250), Math.Max(4, iconSize / 5));
        }

        using (var nameFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold))
        using (var stateFont = new Font("Microsoft YaHei UI", 8.4f))
        using (var nameBrush = new SolidBrush(inkColor))
        using (var stateBrush = new SolidBrush(item.Running ? Color.FromArgb(10, 164, 104) : mutedColor))
        {
            e.Graphics.DrawString(item.DisplayName, nameFont, nameBrush, textLeft,
                e.Bounds.Top + rowHeight * 0.16f);
            string state = item.Running ? "●  正在运行" : "已安装 · 选中后自动打开";
            e.Graphics.DrawString(state, stateFont, stateBrush, textLeft, e.Bounds.Top + rowHeight * 0.52f);
        }

        using (var pen = new Pen(lineColor))
            e.Graphics.DrawLine(pen, textLeft, e.Bounds.Bottom - 1, e.Bounds.Right - 8, e.Bounds.Bottom - 1);
    }

    // The icon for a row, cached by process name. The order is deliberate: what the catalogue already resolved,
    // then the executable of the process itself, then the launch target, and finally a tile generated from the
    // name — which is what guarantees that no row is left with an empty slot.
    private Image IconFor(InstalledAppChoice item)
    {
        string source;
        Image icon = AppIcons.For(item.ProcessName, item.DisplayName, item.LaunchTarget, item.Icon, out source);
        // Reported per application so the coverage can be counted instead of guessed at: which applications still
        // fall through to a generated tile is the question, not whether the chain exists.
        if (source == "catalogue") iconFromCatalogue++;
        else if (source == "process") iconFromProcess++;
        else if (source == "target") iconFromTarget++;
        else if (source == "tile") iconFromTile++;
        // Only the fallbacks are logged per application: the summary below counts every source, and logging all
        // ninety-five rows on every open buried the three that are actually interesting.
        if (logger != null && source == "tile")
        {
            logger("PICKER ICON FALLBACK process=" + item.ProcessName +
                " target=" + (string.IsNullOrWhiteSpace(item.LaunchTarget) ? "-" : "yes") +
                " display=" + item.DisplayName);
        }
        return icon;
    }

    // Filters the machine's own list in place. Running applications stay on top so the most
    // likely choice is still the first row.
    private void ApplyFilter()
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            string needle = (filter.Text ?? "").Trim();
            object previous = applications.SelectedItem;
            applications.BeginUpdate();
            applications.Items.Clear();
            foreach (InstalledAppChoice choice in allChoices)
            {
                if (needle.Length > 0 &&
                    choice.DisplayName.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    choice.ProcessName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                applications.Items.Add(choice);
            }
            applications.EndUpdate();
            emptyState.Visible = applications.Items.Count == 0;
            if (applications.Items.Count == 0)
            {
                hint.Text = "共 " + allChoices.Count + " 个已安装 · 当前筛选没有结果";
                hint.ForeColor = mutedColor;
                applications.SelectedIndex = -1;
                return;
            }
            int restored = previous == null ? -1 : applications.Items.IndexOf(previous);
            applications.SelectedIndex = restored >= 0 ? restored : 0;
            hint.Text = needle.Length == 0
                ? "共 " + allChoices.Count + " 个已安装的应用"
                : "显示 " + applications.Items.Count + " / " + allChoices.Count + " 个应用";
            hint.ForeColor = mutedColor;
        }
        finally
        {
            rebuilding = false;
        }
    }

    private void ConfirmSelection()
    {
        var choice = applications.SelectedItem as InstalledAppChoice;
        if (choice == null || string.IsNullOrWhiteSpace(choice.ProcessName))
        {
            hint.Text = "请先在上面的列表里选择一个应用。";
            hint.ForeColor = Color.FromArgb(204, 70, 82);
            return;
        }
        SelectedProcessName = choice.ProcessName;
        SelectedLaunchTarget = choice.LaunchTarget ?? "";
        SelectedLaunchArguments = choice.Arguments ?? "";
        DialogResult = DialogResult.OK;
        Close();
    }
}
