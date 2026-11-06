using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using SimpleSshLean.Core;

namespace SimpleSshLean.Sshl;

internal static class Launcher
{
    const string Usage = "usage: sshl --host <host> [--port <port>] --user <user> [--name <title>] [-- <extra ssh args>]";

    public static int Run(string[] args)
    {
        if (!TryParse(args, out var target, out var name, out var extra, out var error))
        {
            Console.Error.WriteLine($"sshl: {error}");
            Console.Error.WriteLine(Usage);
            return 2;
        }

        try { Console.Title = name ?? target.Display; } catch (IOException) { }

        // Ctrl+C is ssh's to handle. Without this, a Ctrl+C at the password
        // prompt would also kill the launcher and with it the reconnect offer.
        Console.CancelKeyPress += (_, e) => e.Cancel = true;

        while (true)
        {
            int code = RunSsh(target, extra);
            if (code == 0) return 0;

            Console.WriteLine();
            Console.WriteLine($"[sshl] Connection to {target.Display} ended (exit code {code}).");
            Console.Write("[sshl] Press R to reconnect, or any other key to close. ");
            if (!WaitForReconnect()) return code;
            Console.WriteLine();
            Console.WriteLine();
        }
    }

    static int RunSsh(SshTarget target, IReadOnlyList<string> extra)
    {
        string session = Guid.NewGuid().ToString("N");
        using var gate = new Semaphore(1, 1, AskpassContract.GateName(session));

        var psi = new ProcessStartInfo(AppPaths.SshExe) { UseShellExecute = false };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(target.Port.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(target.Username);
        foreach (var arg in extra) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(target.Host);

        psi.Environment["SSH_ASKPASS"] = Environment.ProcessPath!;
        psi.Environment["SSH_ASKPASS_REQUIRE"] = "force";
        psi.Environment[AskpassContract.SessionVar] = session;
        psi.Environment[AskpassContract.CredentialVar] = CredentialStore.TargetFor(target);

        try
        {
            using var ssh = Process.Start(psi)!;
            ssh.WaitForExit();
            return ssh.ExitCode;
        }
        catch (Win32Exception e)
        {
            Console.Error.WriteLine($"[sshl] Could not start {psi.FileName}: {e.Message}");
            Console.Error.WriteLine("[sshl] Install the OpenSSH Client under Settings > System > Optional features.");
            return -1;
        }
    }

    static bool WaitForReconnect()
    {
        if (Console.IsInputRedirected) return false;
        while (Console.KeyAvailable) Console.ReadKey(intercept: true); // keys typed while the session ended
        return Console.ReadKey(intercept: true).Key == ConsoleKey.R;
    }

    static bool TryParse(string[] args, out SshTarget target, out string? name, out List<string> extra, out string error)
    {
        target = default;
        name = null;
        extra = [];
        string? host = null, user = null;
        int port = SshTarget.DefaultPort;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--")
            {
                extra.AddRange(args[(i + 1)..]);
                break;
            }
            if (i + 1 >= args.Length)
            {
                error = $"{args[i]} needs a value.";
                return false;
            }
            string value = args[++i];
            switch (args[i - 1])
            {
                case "--host": host = value; break;
                case "--user": user = value; break;
                case "--name": name = value; break;
                case "--port" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port): break;
                case "--port":
                    error = "--port must be a number.";
                    return false;
                default:
                    error = $"unknown option {args[i - 1]}.";
                    return false;
            }
        }

        if (SshTarget.Validate(host, port, user) is { } problem)
        {
            error = problem;
            return false;
        }
        target = new SshTarget(host!, port, user!);
        error = "";
        return true;
    }
}
