using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Scales surfaces that are laid out at 96 dpi onto the display they are actually rendered on.
//
// Windows Forms applies its autoscale once, when a window loads. Everything this application builds at
// runtime — pages on navigation, the setup wizard, and every dialog — is built later than that, so their
// absolute coordinates stay at 96 dpi while their fonts follow the display (GDI+ renders point sizes at the
// current DPI). Measured at 200%: page titles collided with their subtitles, the wizard's step names were
// truncated, and the add-application picker drew its title with a doubled font inside a 1x box and cut its
// subtitle off.
//
// Two rules keep this from being applied twice: fonts are never touched (they already follow the display),
// and controls are scaled only once each — the "added later" hook remembers what it has seen, because a
// surface that is refilled on every step or navigation would otherwise grow without bound.
internal static class UiDisplayScale
{
    // Below this, the display is not scaled at all and the 96 dpi layout is already correct.
    internal const float MinimumScale = 1.01f;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr handle);

    // The display scaling of the monitor a control is on, relative to the 96 dpi the layout is designed at.
    internal static float ForControl(Control control)
    {
        if (control == null) return 1f;
        try
        {
            uint dpi = 0;
            if (control.Handle != IntPtr.Zero) dpi = GetDpiForWindow(control.Handle);
            if (dpi < 48 || dpi > 480)
            {
                using (Graphics graphics = control.CreateGraphics()) dpi = (uint)Math.Round(graphics.DpiX);
            }
            if (dpi >= 48 && dpi <= 480) return dpi / 96f;
        }
        catch { }
        return 1f;
    }

    // Prepares a dialog: when it loads, its window and its contents are scaled together, and anything added
    // afterwards is scaled as it arrives. Called from a constructor, so the dialog needs no other change.
    internal static void Apply(Form form)
    {
        if (form == null) return;
        form.Load += delegate
        {
            float scale = ForControl(form);
            if (scale <= MinimumScale) return;
            // The window grows with its content: measured at 200%, the add-application picker stayed at its
            // design size while its fonts doubled, so its title overflowed the box it was drawn in.
            form.Size = new Size(
                (int)Math.Round(form.Width * scale), (int)Math.Round(form.Height * scale));
            Tree(form, scale);
            AddedLater(form, scale);
        };
    }

    // Scales every control inside a parent. Sizes are scaled only for controls that are not auto-sized:
    // an auto-sized label measures itself from its font, which already follows the display.
    internal static void Tree(Control parent, float scale)
    {
        if (parent == null || scale <= MinimumScale) return;
        foreach (Control child in parent.Controls)
        {
            Bounds(child, scale);
            Tree(child, scale);
        }
    }

    // Scales one control's own bounds, honouring its dock and auto-size state.
    internal static void Bounds(Control control, float scale)
    {
        if (control == null || scale <= MinimumScale) return;
        control.Location = new Point(
            (int)Math.Round(control.Left * scale), (int)Math.Round(control.Top * scale));
        // Margins are scaled as well. An anchored control is positioned by the layout engine from the
        // distance to its edge, so an unscaled margin puts it too close to that edge once the parent has
        // grown: measured at 200%, the application picker's count label ran 56x44 px into its 确定 button.
        // A layout container uses the same margins for spacing, where keeping the proportion is what the
        // scaled fonts need anyway.
        control.Margin = new Padding(
            (int)Math.Round(control.Margin.Left * scale), (int)Math.Round(control.Margin.Top * scale),
            (int)Math.Round(control.Margin.Right * scale), (int)Math.Round(control.Margin.Bottom * scale));
        if (control.AutoSize) return;
        int width = (int)Math.Round(control.Width * scale);
        int height = (int)Math.Round(control.Height * scale);
        // A docked control ignores Size and takes its extent from the docked edge, so the one dimension
        // that matters is set explicitly: the sidebar is docked left and stayed 232 px wide at 150%
        // (measured from the application's own diagnostic) while everything around it grew.
        switch (control.Dock)
        {
            case DockStyle.Left:
            case DockStyle.Right: control.Width = width; break;
            case DockStyle.Top:
            case DockStyle.Bottom: control.Height = height; break;
            case DockStyle.None: control.Size = new Size(width, height); break;
        }
    }

    // Scales controls added after this call, at any depth, exactly once each. Surfaces whose content is
    // cleared and refilled — the wizard's step pane, a page rebuilt on navigation — cannot be scaled
    // reliably by a single pass, because their builder returns early from many branches.
    internal static void AddedLater(Control root, float scale)
    {
        if (root == null || scale <= MinimumScale) return;
        InstallOnAdd(root, new HashSet<Control>(), scale);
    }

    private static void InstallOnAdd(Control root, HashSet<Control> alreadyScaled, float scale)
    {
        root.ControlAdded += delegate(object sender, ControlEventArgs e)
        {
            if (e.Control == null || alreadyScaled.Contains(e.Control)) return;
            alreadyScaled.Add(e.Control);
            Bounds(e.Control, scale);
            Tree(e.Control, scale);
            InstallOnAdd(e.Control, alreadyScaled, scale);
        };
        foreach (Control child in root.Controls) InstallOnAdd(child, alreadyScaled, scale);
    }
}
