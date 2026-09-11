using System;
using System.Drawing;
using System.Windows.Forms;

internal static class FocusTargetSmokeApp
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form();
        form.Text = "Vibe Flow Focus Target Smoke";
        form.StartPosition = FormStartPosition.CenterScreen;
        form.Size = new Size(620, 260);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Font = new Font("Microsoft YaHei UI", 10f);

        var title = new Label();
        title.Text = "Smart Focus UI Automation test target";
        title.Location = new Point(28, 28);
        title.Size = new Size(520, 30);
        title.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);

        var input = new TextBox();
        input.Name = "smartFocusSmokeInput";
        input.AccessibleName = "Smart Focus smoke input";
        input.Location = new Point(28, 82);
        input.Size = new Size(540, 34);
        input.TabIndex = 0;

        var note = new Label();
        note.Text = "Local test fixture. No text is read or persisted by Vibe Flow.";
        note.Location = new Point(28, 132);
        note.Size = new Size(540, 28);

        form.Controls.Add(title);
        form.Controls.Add(input);
        form.Controls.Add(note);
        form.Shown += delegate { input.Focus(); };
        Application.Run(form);
    }
}
