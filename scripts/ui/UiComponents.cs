using System;
using System.Drawing;
using System.Windows.Forms;

internal static class VibeWindowLayout
{
    internal static void FitToWorkingArea(Form window, Rectangle workingArea, int margin = 24)
    {
        if (window == null || workingArea.Width <= 0 || workingArea.Height <= 0) return;
        int safeMargin = System.Math.Max(0, margin);
        int availableWidth = System.Math.Max(1, workingArea.Width - safeMargin);
        int availableHeight = System.Math.Max(1, workingArea.Height - safeMargin);
        Size scaledMinimum = window.MinimumSize;
        Size fitted = new Size(
            System.Math.Min(System.Math.Max(1, window.Width), availableWidth),
            System.Math.Min(System.Math.Max(1, window.Height), availableHeight));
        window.MinimumSize = Size.Empty;
        window.Size = fitted;
        window.MinimumSize = new Size(System.Math.Min(scaledMinimum.Width, fitted.Width),
            System.Math.Min(scaledMinimum.Height, fitted.Height));
        window.Location = new Point(
            workingArea.Left + System.Math.Max(0, (workingArea.Width - window.Width) / 2),
            workingArea.Top + System.Math.Max(0, (workingArea.Height - window.Height) / 2));
    }
}

internal sealed partial class VibeMicForm
{
    // Posts an action onto a control's UI thread, refusing controls that were disposed or never got a
    // handle. This lived on the retired FocusTargetDialog and moved here because the Host's DispatchUi
    // — the shared path every background flow uses to touch the UI — is its real production caller, so
    // deleting that dialog must not delete this guard with it.
    internal static bool TryPostToUi(Control control, Action action)
    {
        if (control == null || action == null || control.IsDisposed || !control.IsHandleCreated)
            return false;
        try
        {
            control.BeginInvoke(new Action(delegate
            {
                if (!control.IsDisposed) action();
            }));
            return true;
        }
        catch (ObjectDisposedException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private RoundPanel CreateEmptyStateCard(
        string status,
        string title,
        string description,
        string actionText,
        out Button actionButton)
    {
        var card = NewCard(Point.Empty, new Size(960, 360));
        card.AccessibleName = title;
        card.Padding = new Padding(32);

        var layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.BackColor = Color.Transparent;
        layout.ColumnCount = 1;
        layout.RowCount = 4;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));

        var statusLabel = NewLabel(status, 8.5f, FontStyle.Bold, violet);
        statusLabel.AccessibleName = status;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var titleLabel = NewLabel(title, 20f, FontStyle.Bold, ink);
        titleLabel.AccessibleName = title;
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        var descriptionLabel = NewLabel(description, 10f, FontStyle.Regular, muted);
        descriptionLabel.AccessibleName = description;
        descriptionLabel.Dock = DockStyle.Fill;
        descriptionLabel.TextAlign = ContentAlignment.TopLeft;
        descriptionLabel.AutoEllipsis = true;

        var actions = new FlowLayoutPanel();
        actions.Dock = DockStyle.Fill;
        actions.FlowDirection = FlowDirection.LeftToRight;
        actions.WrapContents = false;
        actions.BackColor = Color.Transparent;
        actions.Margin = Padding.Empty;
        actions.Padding = Padding.Empty;

        actionButton = PrimaryButton(actionText, Point.Empty, new Size(154, 42));
        actionButton.AccessibleName = actionText;
        actionButton.Margin = new Padding(0, 6, 0, 0);
        actions.Controls.Add(actionButton);

        layout.Controls.Add(statusLabel, 0, 0);
        layout.Controls.Add(titleLabel, 0, 1);
        layout.Controls.Add(descriptionLabel, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        card.Controls.Add(layout);
        return card;
    }
}
