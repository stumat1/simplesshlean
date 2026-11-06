using System.Text;
using SimpleSshLean.Core;

namespace SimpleSshLean.Sshl;

/// <summary>
/// ssh's SSH_ASKPASS helper. ssh runs it with the prompt as its argument and
/// reads the answer from its stdout. A login password prompt gets the saved
/// password once; everything else (host-key confirmation, a rejected saved
/// password, passphrases, OTP codes) is asked right in the terminal, as plain
/// ssh would.
/// </summary>
internal static class Askpass
{
    public static int Run(string[] args, string session)
    {
        string prompt = args.Length > 0 ? string.Join(' ', args) : "Password: ";

        if (AskpassContract.IsPasswordPrompt(prompt) && TakeSavedPassword(session) is { } saved)
            return Answer(saved);

        var answer = ConsoleIO.Prompt(prompt, echo: AskpassContract.IsYesNoPrompt(prompt));
        return answer is null ? 1 : Answer(answer);
    }

    static string? TakeSavedPassword(string session)
    {
        var target = Environment.GetEnvironmentVariable(AskpassContract.CredentialVar);
        if (string.IsNullOrEmpty(target)) return null;
        var saved = CredentialStore.Read(target);
        if (saved is null) return null;

        if (Semaphore.TryOpenExisting(AskpassContract.GateName(session), out var gate))
        {
            using (gate)
            {
                // Deliberately never released: one saved-password answer per attempt.
                if (gate.WaitOne(0)) return saved;
            }
        }
        ConsoleIO.Notice("The saved password was not accepted.");
        return null;
    }

    static int Answer(string text)
    {
        // Raw UTF-8 bytes: Console.Out would re-encode to the console code page.
        var bytes = Encoding.UTF8.GetBytes(text + "\n");
        try
        {
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
        return 0;
    }
}
