using System.Diagnostics;
using System.Globalization;
using SimpleSshLean.Core;

namespace SimpleSshLean;

/// <summary>Opens a connection as a Windows Terminal tab, or in a console window if Terminal isn't installed.</summary>
internal static class TerminalLauncher
{
    /// <summary>Returns true when the connection opened in Windows Terminal.</summary>
    public static bool Open(SshTarget target, string title)
    {
        string sshl = Path.Combine(AppContext.BaseDirectory, "sshl.exe");
        if (!File.Exists(sshl)) throw new FileNotFoundException("sshl.exe is missing from the app folder.", sshl);

        title = Sanitize(title);
        string[] command =
        [
            sshl, "--host", target.Host, "--port", target.Port.ToString(CultureInfo.InvariantCulture),
            "--user", target.Username, "--name", title,
        ];

        var wt = FindWindowsTerminal();
        ProcessStartInfo psi;
        if (wt is not null)
        {
            psi = new ProcessStartInfo(wt) { UseShellExecute = false };
            // -w 0: a new tab in the most recently used Terminal window (or a new window if none is open).
            string[] wtArgs = ["-w", "0", "new-tab", "--title", title, "--", .. command];
            foreach (var arg in wtArgs) psi.ArgumentList.Add(EscapeForWt(arg));
        }
        else
        {
            // A console app started from a GUI app gets its own console window.
            psi = new ProcessStartInfo(sshl) { UseShellExecute = false };
            foreach (var arg in command[1..]) psi.ArgumentList.Add(arg);
        }

        using var _ = Process.Start(psi);
        return wt is not null;
    }

    static string? FindWindowsTerminal()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(alias)) return alias;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), "wt.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Quotes and control characters don't survive Terminal's command-line re-quoting.</summary>
    static string Sanitize(string text) => new string(text.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();

    /// <summary>wt.exe treats ';' as a command separator anywhere in its arguments.</summary>
    static string EscapeForWt(string arg) => arg.Replace(";", @"\;");
}
