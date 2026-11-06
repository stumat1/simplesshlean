using SimpleSshLean.Core;

namespace SimpleSshLean;

/// <summary>Add or edit a saved host, including its saved password.</summary>
internal sealed class HostDialog : Form
{
    readonly string id;
    readonly TextBox nameBox = new() { Width = 260 };
    readonly TextBox hostBox = new() { Width = 260 };
    readonly NumericUpDown portBox = new() { Minimum = 1, Maximum = 65535, Value = SshTarget.DefaultPort, Width = 90 };
    readonly TextBox userBox = new() { Width = 260 };
    readonly TextBox passwordBox = new() { Width = 260, UseSystemPasswordChar = true };
    readonly CheckBox rememberBox = new() { Text = "Remember password in Windows Credential Manager", AutoSize = true };

    public HostEntry Result { get; private set; } = null!;

    public bool RememberPassword => rememberBox.Checked;

    /// <summary>Empty means "unchanged".</summary>
    public string NewPassword => passwordBox.Text;

    public HostDialog(HostEntry? existing, bool hasSavedPassword)
    {
        id = existing?.Id ?? "";
        Text = existing is null ? "Add host" : "Edit host";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        if (existing is not null)
        {
            nameBox.Text = existing.Name;
            hostBox.Text = existing.Host;
            portBox.Value = existing.Port;
            userBox.Text = existing.Username;
        }
        rememberBox.Checked = hasSavedPassword;
        if (hasSavedPassword) passwordBox.PlaceholderText = "Saved (leave blank to keep it)";
        nameBox.PlaceholderText = "Optional, e.g. NAS";
        passwordBox.TextChanged += (_, _) => { if (passwordBox.TextLength > 0) rememberBox.Checked = true; };

        var ok = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Accept();
        AcceptButton = ok;
        CancelButton = cancel;

        var fields = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(fields, "Name", nameBox);
        AddRow(fields, "Host", hostBox);
        AddRow(fields, "Port", portBox);
        AddRow(fields, "User", userBox);
        AddRow(fields, "Password", passwordBox);
        fields.Controls.Add(new Label(), 0, fields.RowCount);
        fields.Controls.Add(rememberBox, 1, fields.RowCount++);
        fields.Controls.Add(new Label
        {
            Text = "Leave the password empty to type it in the terminal,\nor to use your SSH keys and agent instead.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 6),
        }, 1, fields.RowCount++);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 12, 12),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(fields, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
    }

    static void AddRow(TableLayoutPanel table, string label, Control field)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, table.RowCount);
        field.Margin = new Padding(3, 3, 3, 3);
        table.Controls.Add(field, 1, table.RowCount++);
    }

    void Accept()
    {
        string host = hostBox.Text.Trim(), user = userBox.Text.Trim();
        int port = (int)portBox.Value;
        if (SshTarget.Validate(host, port, user) is { } problem)
        {
            MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Result = new HostEntry(id, nameBox.Text.Trim(), host, port, user);
        DialogResult = DialogResult.OK;
    }
}
