using System.Diagnostics;
using SimpleSshLean.Core;

namespace SimpleSshLean;

/// <summary>The host list. Connections themselves run in Windows Terminal.</summary>
internal sealed class MainForm : Form
{
    readonly HostStore store;
    readonly TextBox quickBox = new() { PlaceholderText = "user@host or user@host:port", Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 3) };
    readonly ListView list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        Dock = DockStyle.Fill,
    };
    readonly Button connectButton = new() { Text = "Connect", AutoSize = true };
    readonly Button editButton = new() { Text = "Edit…", AutoSize = true };
    readonly Button deleteButton = new() { Text = "Delete", AutoSize = true };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripMenuItem forgetPasswordItem = new("Forget saved password");

    public MainForm(HostStore store, string? startupMessage)
    {
        this.store = store;
        Text = "SimpleSSH Lean";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(560, 380);
        MinimumSize = new Size(420, 280);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var quickButton = new Button { Text = "Connect", AutoSize = true };
        quickButton.Click += (_, _) => QuickConnect();
        quickBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            QuickConnect();
        };
        var quick = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        quick.Controls.Add(new Label { Text = "Quick connect", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        quick.Controls.Add(quickBox, 1, 0);
        quick.Controls.Add(quickButton, 2, 0);

        list.Columns.Add("Name");
        list.Columns.Add("Target");
        list.Columns.Add("Password");
        list.Resize += (_, _) => SizeColumns();
        list.DoubleClick += (_, _) => ConnectSelected();
        list.SelectedIndexChanged += (_, _) => UpdateButtons();
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ConnectSelected(); }
            else if (e.KeyCode == Keys.Delete) DeleteSelected();
            else if (e.KeyCode == Keys.F2) EditSelected();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Connect", null, (_, _) => ConnectSelected());
        menu.Items.Add("Edit…", null, (_, _) => EditSelected());
        menu.Items.Add("Delete", null, (_, _) => DeleteSelected());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(forgetPasswordItem);
        menu.Items.Add("Forget trusted host key…", null, (_, _) => ForgetHostKey());
        forgetPasswordItem.Click += (_, _) => ForgetPassword();
        menu.Opening += (_, e) =>
        {
            if (Selected is not { } h) { e.Cancel = true; return; }
            forgetPasswordItem.Enabled = CredentialStore.Exists(CredentialStore.TargetFor(h.Target));
        };
        list.ContextMenuStrip = menu;

        var addButton = new Button { Text = "Add…", AutoSize = true };
        addButton.Click += (_, _) => AddOrEdit(null);
        connectButton.Click += (_, _) => ConnectSelected();
        editButton.Click += (_, _) => EditSelected();
        deleteButton.Click += (_, _) => DeleteSelected();
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        buttons.Controls.AddRange([connectButton, addButton, editButton, deleteButton]);

        var root = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(8) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(quick, 0, 0);
        root.Controls.Add(list, 0, 1);
        root.Controls.Add(buttons, 0, 2);

        var statusStrip = new StatusStrip { SizingGrip = false };
        statusStrip.Items.Add(status);
        Controls.Add(root);
        Controls.Add(statusStrip);

        Reload();
        SetStatus(startupMessage ?? (store.Hosts.Count == 0 ? "No saved hosts yet. Add one, or use quick connect." : null));
        Shown += (_, _) => (store.Hosts.Count > 0 ? list : (Control)quickBox).Focus();
    }

    HostEntry? Selected => list.SelectedItems.Count == 1 ? (HostEntry)list.SelectedItems[0].Tag! : null;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.N)) { AddOrEdit(null); return true; }
        if (keyData == (Keys.Control | Keys.L)) { quickBox.Focus(); quickBox.SelectAll(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void Reload(string? selectId = null)
    {
        selectId ??= Selected?.Id;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var h in store.Hosts.OrderBy(h => h.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            bool saved = CredentialStore.Exists(CredentialStore.TargetFor(h.Target));
            var item = new ListViewItem([h.DisplayName, h.Target.Display, saved ? "Saved" : ""]) { Tag = h };
            list.Items.Add(item);
            if (h.Id == selectId) item.Selected = item.Focused = true;
        }
        if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = list.Items[0].Focused = true;
        list.EndUpdate();
        SizeColumns();
        UpdateButtons();
    }

    void SizeColumns()
    {
        if (list.Columns.Count < 3) return;
        int passwordWidth = LogicalToDeviceUnits(80);
        int rest = Math.Max(0, list.ClientSize.Width - passwordWidth);
        list.Columns[0].Width = rest * 2 / 5;
        list.Columns[1].Width = rest - list.Columns[0].Width;
        list.Columns[2].Width = passwordWidth;
    }

    void UpdateButtons()
    {
        bool any = Selected is not null;
        connectButton.Enabled = any;
        editButton.Enabled = any && !store.IsReadOnly;
        deleteButton.Enabled = any && !store.IsReadOnly;
    }

    void SetStatus(string? text) => status.Text = text ?? "";

    void QuickConnect()
    {
        if (!SshTarget.TryParse(quickBox.Text, out var target, out var error))
        {
            SetStatus(error);
            quickBox.Focus();
            return;
        }
        Connect(target, target.Display);
    }

    void ConnectSelected()
    {
        if (Selected is { } h) Connect(h.Target, h.DisplayName);
    }

    void Connect(SshTarget target, string title)
    {
        try
        {
            bool inTerminal = TerminalLauncher.Open(target, title);
            SetStatus(inTerminal ? $"Opened {target.Display} in Windows Terminal." : $"Opened {target.Display} in a console window.");
        }
        catch (Exception e)
        {
            ShowError($"Could not open a terminal for {target.Display}", e);
        }
    }

    void EditSelected()
    {
        if (Selected is { } h) AddOrEdit(h);
    }

    void AddOrEdit(HostEntry? existing)
    {
        if (store.IsReadOnly) { SetStatus(store.Warning); return; }
        bool hasSaved = existing is not null && CredentialStore.Exists(CredentialStore.TargetFor(existing.Target));
        using var dialog = new HostDialog(existing, hasSaved);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var saved = store.Save(dialog.Result);
            ApplyPassword(existing?.Target, saved, dialog.RememberPassword, dialog.NewPassword);
            Reload(saved.Id);
            SetStatus($"Saved {saved.DisplayName}.");
        }
        catch (Exception e)
        {
            ShowError("Could not save the host", e);
            Reload();
        }
    }

    /// <summary>Passwords are keyed by user@host:port, so editing those fields moves the saved password along.</summary>
    void ApplyPassword(SshTarget? oldTarget, HostEntry saved, bool remember, string newPassword)
    {
        var newKey = CredentialStore.TargetFor(saved.Target);
        var oldKey = oldTarget is { } old ? CredentialStore.TargetFor(old) : null;
        bool moved = oldKey is not null && oldKey != newKey;

        if (!remember) CredentialStore.Delete(newKey);
        else if (newPassword.Length > 0) CredentialStore.Write(newKey, saved.Username, newPassword);
        else if (moved && CredentialStore.Read(oldKey!) is { } carried) CredentialStore.Write(newKey, saved.Username, carried);

        if (moved && !store.Hosts.Any(h => h.Target == oldTarget)) CredentialStore.Delete(oldKey!);
    }

    void DeleteSelected()
    {
        if (Selected is not { } h || store.IsReadOnly) return;
        var answer = MessageBox.Show(this, $"Delete {h.DisplayName} ({h.Target.Display})?\n\nIts saved password is removed too.",
            "Delete host", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        try
        {
            store.Delete(h.Id);
            if (!store.Hosts.Any(other => other.Target == h.Target)) CredentialStore.Delete(CredentialStore.TargetFor(h.Target));
            Reload();
            SetStatus($"Deleted {h.DisplayName}.");
        }
        catch (Exception e)
        {
            ShowError("Could not delete the host", e);
        }
    }

    void ForgetPassword()
    {
        if (Selected is not { } h) return;
        try
        {
            CredentialStore.Delete(CredentialStore.TargetFor(h.Target));
            Reload();
            SetStatus($"Forgot the saved password for {h.Target.Display}.");
        }
        catch (Exception e)
        {
            ShowError("Could not remove the saved password", e);
        }
    }

    /// <summary>
    /// ssh refuses to connect when a host key changes. After a known, legitimate
    /// change (e.g. a reinstall) the old key has to be removed from known_hosts.
    /// </summary>
    void ForgetHostKey()
    {
        if (Selected is not { } h) return;
        var name = h.Target.KnownHostsName;
        var answer = MessageBox.Show(this,
            $"Remove the trusted host key for {name} from your known_hosts file?\n\n" +
            "Only do this if you know why the key changed, for example because the machine was reinstalled. " +
            "ssh will ask you to verify the new key on the next connection.",
            "Forget host key", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            var psi = new ProcessStartInfo(AppPaths.SshKeygenExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-R");
            psi.ArgumentList.Add(name);
            using var keygen = Process.Start(psi)!;
            var stderr = keygen.StandardError.ReadToEndAsync();
            var output = (keygen.StandardOutput.ReadToEnd() + stderr.Result).Trim();
            keygen.WaitForExit();
            MessageBox.Show(this, output.Length > 0 ? output : $"Host key for {name} removed.", "Forget host key",
                MessageBoxButtons.OK, keygen.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception e)
        {
            ShowError("Could not run ssh-keygen", e);
        }
    }

    void ShowError(string what, Exception e) =>
        MessageBox.Show(this, $"{what}.\n\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
