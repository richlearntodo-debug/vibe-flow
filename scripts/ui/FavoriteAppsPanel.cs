using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Consumer surface for Smart Focus. Design rules:
//   * every row is numbered (1, 2, 3 ...) and shows one big primary action plus delete;
//   * the success feedback IS the save entry: a freshly captured application gets a tall
//     green acknowledgement banner that carries the 「保存」 button, so a save can never
//     happen without the user having been shown the success message;
//   * the mode (shortcut / workflow) is a small clickable pill, not a sentence;
//   * no text fields, no strategies, no error codes: those stay in the log.
internal static class FavoriteAppsPanel
{
    internal const int RowHeight = 88;
    internal const int BannerHeight = 96;

    private static readonly Color Ink = Color.FromArgb(23, 29, 45);
    private static readonly Color Muted = Color.FromArgb(112, 120, 138);
    private static readonly Color Faint = Color.FromArgb(160, 168, 184);
    private static readonly Color Line = Color.FromArgb(230, 234, 243);
    private static readonly Color Accent = Color.FromArgb(88, 86, 214);
    private static readonly Color AccentSoft = Color.FromArgb(240, 239, 253);
    private static readonly Color Ready = Color.FromArgb(240, 251, 245);
    private static readonly Color ReadyInk = Color.FromArgb(20, 128, 86);
    private static readonly Color ReadyLine = Color.FromArgb(198, 233, 214);
    private static readonly Color Card = Color.White;
    private static readonly Color Number = Color.FromArgb(226, 230, 240);

    internal const string ShortcutModeLabel = "快捷键";
    internal const string WorkflowModeLabel = "工作流";

    internal static bool IsWorkflowMode(string mode)
    {
        return string.Equals((mode ?? "").Trim(), "workflow", StringComparison.OrdinalIgnoreCase);
    }

    internal static int MeasureHeight(int rowCount, bool hasBanner)
    {
        if (rowCount <= 0 && !hasBanner) return 186;
        return 96 + (hasBanner ? BannerHeight + 12 : 0) + rowCount * RowHeight + 16;
    }

    internal static Control Build(IList<FavoriteApp> apps, string selectedProcess, string pendingProcess,
        Action<string> onLearn, Action<string> onSave, Action<string> onOpen, Action<string> onRelearn,
        Action<string> onDelete, Action<string> onToggleMode, Action<string> onMakeCurrent,
        Action<string> onEdit, Func<string, FavoriteAppState> stateOf, Action onAdd)
    {
        int count = apps == null ? 0 : apps.Count;
        bool hasBanner = !string.IsNullOrWhiteSpace(pendingProcess);
        var panel = new Panel
        {
            BackColor = Card,
            Size = new Size(924, MeasureHeight(count, hasBanner))
        };

        panel.Controls.Add(new Label
        {
            Text = "常用应用",
            Location = new Point(24, 20),
            Size = new Size(320, 32),
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            ForeColor = Ink
        });

        Button addButton = MakeButton("＋ 添加应用", Accent, Color.White);
        addButton.Location = new Point(panel.Width - addButton.Width - 24, 18);
        addButton.Click += delegate { if (onAdd != null) onAdd(); };
        panel.Controls.Add(addButton);

        if (count == 0 && !hasBanner)
        {
            panel.Controls.Add(new Label
            {
                Text = "还没有常用应用",
                Location = new Point(26, 78),
                Size = new Size(520, 30),
                Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold),
                ForeColor = Ink
            });
            panel.Controls.Add(new Label
            {
                Text = "点右上角「添加应用」，从正在运行的应用里选一个，再点一下它的输入框即可。",
                Location = new Point(26, 112),
                Size = new Size(760, 26),
                Font = new Font("Microsoft YaHei UI", 9.5f),
                ForeColor = Muted
            });
            return panel;
        }

        int top = 62;
        if (hasBanner)
        {
            panel.Controls.Add(BuildBanner(panel.Width, apps, pendingProcess, onSave));
            top += BannerHeight + 12;
        }
        else
        {
            panel.Controls.Add(new Label
            {
                Text = "按住录音键时，文字会进入标着「当前」的那个应用。",
                Location = new Point(26, 58),
                Size = new Size(700, 24),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Faint
            });
        }

        for (int index = 0; index < count; index++)
        {
            FavoriteApp app = apps[index];
            if (app == null) continue;
            bool isCurrent = !string.IsNullOrWhiteSpace(selectedProcess) &&
                string.Equals(app.processName, selectedProcess, StringComparison.OrdinalIgnoreCase);
            bool pending = !string.IsNullOrWhiteSpace(pendingProcess) &&
                string.Equals(app.processName, pendingProcess, StringComparison.OrdinalIgnoreCase);
            FavoriteAppState state = stateOf == null
                ? (string.IsNullOrWhiteSpace(app.targetId) ? FavoriteAppState.NotLearned : FavoriteAppState.Verified)
                : stateOf(app.processName);
            panel.Controls.Add(BuildRow(panel.Width, app, index + 1, isCurrent, state, pending, top,
                onLearn, onSave, onOpen, onRelearn, onDelete, onToggleMode, onMakeCurrent, onEdit));
            top += RowHeight;
        }
        return panel;
    }

    // The acknowledgement that learning succeeded, with the save action inside it: the user
    // cannot reach 「保存」 without having been shown the success state.
    private static Control BuildBanner(int width, IList<FavoriteApp> apps, string pendingProcess,
        Action<string> onSave)
    {
        string name = pendingProcess;
        if (apps != null)
        {
            foreach (FavoriteApp app in apps)
            {
                if (app == null) continue;
                if (string.Equals(app.processName, pendingProcess, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(app.displayName))
                {
                    name = app.displayName;
                    break;
                }
            }
        }

        var banner = new Panel
        {
            Location = new Point(24, 62),
            Size = new Size(width - 48, BannerHeight),
            BackColor = Ready
        };
        banner.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(ReadyLine)) e.Graphics.DrawRectangle(pen, 0, 0, banner.Width - 1, banner.Height - 1);
            using (var brush = new SolidBrush(ReadyInk)) e.Graphics.FillRectangle(brush, 0, 0, 4, banner.Height);
        };

        banner.Controls.Add(new Label
        {
            Text = "✓  学习成功",
            Location = new Point(22, 16),
            Size = new Size(200, 26),
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            ForeColor = ReadyInk
        });
        banner.Controls.Add(new Label
        {
            Text = "已经识别到「" + name + "」，点「保存」完成设置。",
            Location = new Point(22, 46),
            Size = new Size(banner.Width - 220, 28),
            Font = new Font("Microsoft YaHei UI", 9.5f),
            ForeColor = Ink
        });

        Button save = MakeButton("保存", ReadyInk, Color.White);
        save.Location = new Point(banner.Width - save.Width - 20, (BannerHeight - save.Height) / 2);
        save.Click += delegate { if (onSave != null) onSave(pendingProcess); };
        banner.Controls.Add(save);
        return banner;
    }

    private static Control BuildRow(int width, FavoriteApp app, int number, bool isCurrent,
        FavoriteAppState state, bool pending, int top, Action<string> onLearn, Action<string> onSave,
        Action<string> onOpen, Action<string> onRelearn, Action<string> onDelete,
        Action<string> onToggleMode, Action<string> onMakeCurrent, Action<string> onEdit)
    {
        string name = string.IsNullOrWhiteSpace(app.displayName) ? app.processName : app.displayName;
        string process = app.processName;
        bool workflow = IsWorkflowMode(app.mode);
        bool learned = state == FavoriteAppState.Verified || state == FavoriteAppState.LearnedUnverified;
        var row = new Panel
        {
            Location = new Point(0, top),
            Size = new Size(width, RowHeight),
            BackColor = isCurrent ? Ready : Card
        };
        row.Paint += delegate(object sender, PaintEventArgs e)
        {
            if (isCurrent)
            {
                using (var brush = new SolidBrush(ReadyInk)) e.Graphics.FillRectangle(brush, 24, 14, 4, row.Height - 28);
            }
            using (var pen = new Pen(Line)) e.Graphics.DrawLine(pen, 24, row.Height - 1, row.Width - 24, row.Height - 1);
        };

        var badge = new Label
        {
            Text = number.ToString(),
            Location = new Point(40, (RowHeight - 30) / 2),
            Size = new Size(30, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            ForeColor = isCurrent ? ReadyInk : Muted,
            BackColor = isCurrent ? Card : Number
        };
        row.Controls.Add(badge);

        row.Controls.Add(new Label
        {
            Text = name,
            Location = new Point(84, 16),
            Size = new Size(320, 28),
            Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold),
            ForeColor = Ink
        });

        if (isCurrent)
        {
            row.Controls.Add(new Label
            {
                Text = "✓ 当前",
                Location = new Point(412, 18),
                Size = new Size(76, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Bold),
                ForeColor = ReadyInk
            });
        }

        var modePill = new Label
        {
            Text = workflow ? WorkflowModeLabel : ShortcutModeLabel,
            Location = new Point(84, 48),
            Size = new Size(74, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 8.6f, FontStyle.Bold),
            ForeColor = workflow ? Color.White : Accent,
            BackColor = workflow ? Accent : AccentSoft,
            Cursor = Cursors.Hand
        };
        modePill.Click += delegate { if (onToggleMode != null) onToggleMode(process); };
        var modeTip = new ToolTip();
        modeTip.SetToolTip(modePill, FavoriteAppStatus.ModeTooltip(workflow) + "（点一下切换）");
        modePill.Tag = modeTip;
        row.Controls.Add(modePill);

        // One honest line per row: it says which state the entry is in and what to do about it, instead of
        // promising delivery that only a current workflow entry actually performs. A freshly captured entry
        // is not saved yet, so it keeps its own "waiting for 保存" line rather than reading as unlearned.
        row.Controls.Add(new Label
        {
            Text = pending ? "等待你点「保存」" : FavoriteAppStatus.DescribeForRow(state, isCurrent, workflow),
            Location = new Point(170, 48),
            Size = new Size(Math.Max(140, width - 610), 24),
            Font = new Font("Microsoft YaHei UI", 9.2f),
            ForeColor = pending ? Accent : isCurrent ? ReadyInk : Muted
        });

        Button makeCurrent = MakeButton(isCurrent ? "已是当前" : "设为当前", Color.White, Ink);
        makeCurrent.Size = new Size(96, 40);
        makeCurrent.Location = new Point(width - 424, (RowHeight - makeCurrent.Height) / 2);
        makeCurrent.FlatAppearance.BorderColor = Line;
        makeCurrent.Enabled = FavoriteAppStatus.CanBecomeCurrent(state) && !isCurrent;
        var currentTip = new ToolTip();
        currentTip.SetToolTip(makeCurrent, isCurrent
            ? "这个应用已经在接收文字"
            : FavoriteAppStatus.CanBecomeCurrent(state)
                ? (workflow ? "让文字固定进入它" : "让文字固定进入它（会同时把模式切到工作流）")
                : "先学习这个应用的输入框，才能让它接收文字");
        makeCurrent.Tag = currentTip;
        makeCurrent.Click += delegate { if (onMakeCurrent != null) onMakeCurrent(process); };
        row.Controls.Add(makeCurrent);

        Button primary;
        if (pending || state == FavoriteAppState.TargetMissing)
        {
            primary = MakeButton("重新学习", Color.White, Ink);
            primary.FlatAppearance.BorderColor = Line;
            primary.Click += delegate { if (onRelearn != null) onRelearn(process); };
        }
        else if (isCurrent)
        {
            primary = MakeButton("重新学习", Color.White, Ink);
            primary.FlatAppearance.BorderColor = Line;
            primary.Click += delegate { if (onRelearn != null) onRelearn(process); };
        }
        else if (learned)
        {
            primary = MakeButton("打开", Accent, Color.White);
            primary.Click += delegate { if (onOpen != null) onOpen(process); };
        }
        else
        {
            primary = MakeButton("学习", Accent, Color.White);
            primary.Click += delegate { if (onLearn != null) onLearn(process); };
        }
        primary.Location = new Point(width - 320, (RowHeight - primary.Height) / 2);
        row.Controls.Add(primary);

        Button edit = MakeButton("编辑", Color.White, Ink);
        edit.Size = new Size(72, 40);
        edit.Location = new Point(width - 192, (RowHeight - edit.Height) / 2);
        edit.FlatAppearance.BorderColor = Line;
        var editTip = new ToolTip();
        editTip.SetToolTip(edit, "改名、切换模式、查看目标详情、测试焦点");
        edit.Tag = editTip;
        edit.Click += delegate { if (onEdit != null) onEdit(process); };
        row.Controls.Add(edit);

        Button delete = MakeButton("删除", Color.White, Faint);
        delete.Size = new Size(88, 40);
        delete.Location = new Point(width - 112, (RowHeight - delete.Height) / 2);
        delete.FlatAppearance.BorderColor = Line;
        delete.Click += delegate { if (onDelete != null) onDelete(process); };
        row.Controls.Add(delete);
        return row;
    }

    private static Button MakeButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(120, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
        button.FlatAppearance.BorderColor = Line;
        return button;
    }
}
