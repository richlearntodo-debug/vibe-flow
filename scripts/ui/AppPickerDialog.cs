using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// The redesigned Smart Focus entry point. It lists both what is running right now and what
// is merely installed, because a favourite is allowed to be an application the user has not
// started yet: picking it launches it and then learns its input box.
//
// The only text field in the whole feature lives here and it is a filter: it narrows the
// machine's own list, it never becomes user content and it is never stored.
internal sealed class AppPickerDialog : Form
{
    private readonly ListBox applications = new ListBox();
    private readonly TextBox filter = new TextBox();
    private readonly Label hint = new Label();
    private readonly List<InstalledAppChoice> allChoices = new List<InstalledAppChoice>();
    private bool rebuilding;

    internal string SelectedProcessName { get; private set; }
    internal string SelectedLaunchTarget { get; private set; }
    internal string SelectedLaunchArguments { get; private set; }

    internal AppPickerDialog(IList<InstalledAppChoice> choices)
    {
        // Its layout is built at 96 dpi at runtime, so it is scaled onto the display it opens on: measured
        // at 200%, this dialog drew its title with a doubled font inside a 1x box and cut its subtitle off.
        UiDisplayScale.Apply(this);
        SelectedProcessName = "";
        SelectedLaunchTarget = "";
        SelectedLaunchArguments = "";
        Text = "添加应用";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 620);
        Size = new Size(580, 660);
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        Controls.Add(new Label
        {
            Text = "选择要添加的应用",
            Location = new Point(20, 18),
            Size = new Size(500, 30),
            Font = new Font("Microsoft YaHei UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(23, 29, 45)
        });
        Controls.Add(new Label
        {
            Text = "上面是正在运行的应用，下面是已安装的；筛选用不着记住名字，未运行的点「确定」会自动打开。",
            Location = new Point(20, 50),
            Size = new Size(530, 24),
            ForeColor = Color.FromArgb(112, 120, 138)
        });

        var filterCaption = new Label
        {
            Text = "筛选",
            Location = new Point(20, 82),
            Size = new Size(40, 24),
            ForeColor = Color.FromArgb(112, 120, 138)
        };
        Controls.Add(filterCaption);

        filter.Location = new Point(64, 79);
        filter.Size = new Size(496, 26);
        filter.BorderStyle = BorderStyle.FixedSingle;
        filter.TextChanged += delegate { ApplyFilter(); };
        Controls.Add(filter);

        applications.Location = new Point(20, 116);
        applications.Size = new Size(540, 440);
        applications.IntegralHeight = false;
        applications.DrawMode = DrawMode.OwnerDrawFixed;
        applications.ItemHeight = 48;
        applications.DrawItem += delegate(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= applications.Items.Count) return;
            var item = applications.Items[e.Index] as InstalledAppChoice;
            if (item == null) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color ink = selected ? Color.White : Color.FromArgb(23, 29, 45);
            Color muted = selected ? Color.FromArgb(235, 235, 250) : Color.FromArgb(122, 130, 148);
            using (var nameFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold))
            using (var stateFont = new Font("Microsoft YaHei UI", 8.4f))
            using (var nameBrush = new SolidBrush(ink))
            using (var stateBrush = new SolidBrush(muted))
            {
                if (item.Icon != null) e.Graphics.DrawIcon(item.Icon, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 8, 32, 32));
                e.Graphics.DrawString((e.Index + 1).ToString("00") + "    " + item.DisplayName, nameFont, nameBrush, e.Bounds.Left + 50, e.Bounds.Top + 6);
                e.Graphics.DrawString(item.Running ? "正在运行" : "未运行 · 选中后自动打开", stateFont, stateBrush, e.Bounds.Left + 50, e.Bounds.Top + 26);
            }
        };
        applications.DoubleClick += delegate { ConfirmSelection(); };
        Controls.Add(applications);

        hint.Location = new Point(20, 566);
        // The box stops short of the confirm button, which starts at x=332: a 340 px box overlapped it by
        // 28 px at every display scaling (measured with scripts/check-ui-geometry.ps1 -WindowTitle, which
        // reports 56x44 px at 200%). The count text is short enough today that the two do not visibly
        // touch, but a longer count would run under the button.
        hint.Size = new Size(296, 22);
        hint.ForeColor = Color.FromArgb(112, 120, 138);
        Controls.Add(hint);

        var confirm = new Button
        {
            Text = "确定",
            Location = new Point(332, 562),
            Size = new Size(104, 36),
            FlatStyle = FlatStyle.System
        };
        confirm.Click += delegate { ConfirmSelection(); };
        Controls.Add(confirm);

        var cancel = new Button
        {
            Text = "取消",
            Location = new Point(444, 562),
            Size = new Size(104, 36),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancel);
        AcceptButton = confirm;
        CancelButton = cancel;

        if (choices != null)
        {
            foreach (InstalledAppChoice choice in choices)
            {
                if (choice != null && !string.IsNullOrWhiteSpace(choice.ProcessName)) allChoices.Add(choice);
            }
        }
        ApplyFilter();
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
            if (applications.Items.Count == 0)
            {
                hint.Text = "没有匹配的应用，换个词试试。";
                hint.ForeColor = Color.FromArgb(196, 92, 78);
                return;
            }
            int restored = previous == null ? -1 : applications.Items.IndexOf(previous);
            applications.SelectedIndex = restored >= 0 ? restored : 0;
            hint.Text = "共 " + applications.Items.Count + " 个可选";
            hint.ForeColor = Color.FromArgb(112, 120, 138);
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
            hint.ForeColor = Color.FromArgb(196, 92, 78);
            return;
        }
        SelectedProcessName = choice.ProcessName;
        SelectedLaunchTarget = choice.LaunchTarget ?? "";
        SelectedLaunchArguments = choice.Arguments ?? "";
        DialogResult = DialogResult.OK;
        Close();
    }
}
