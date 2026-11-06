namespace SimpleSshLean.Core;

/// <summary>
/// How the sshl launcher and its askpass mode talk to each other. The launcher
/// sets these variables on the ssh process; ssh passes them on to the askpass
/// helper it spawns. No secret ever goes through the environment.
/// </summary>
public static class AskpassContract
{
    /// <summary>Per-connection-attempt id. Its presence is what puts sshl into askpass mode.</summary>
    public const string SessionVar = "SIMPLESSHLEAN_SESSION";

    /// <summary>Credential Manager target name holding this connection's saved password.</summary>
    public const string CredentialVar = "SIMPLESSHLEAN_CREDENTIAL";

    /// <summary>
    /// A named semaphore the launcher holds for each attempt. Askpass takes it
    /// before answering with the saved password, so the saved password is sent
    /// at most once; a second password prompt means it was rejected.
    /// </summary>
    public static string GateName(string session) => $@"Local\SimpleSshLean.{session}";

    static readonly string[] NotTheLoginPassword = ["new ", "retype", "again", "confirm"];

    /// <summary>
    /// True for ssh's login password prompts ("user@host's password:" and the
    /// keyboard-interactive "Password:"). Key passphrases, OTP codes and
    /// password-change prompts all go to the user.
    /// </summary>
    public static bool IsPasswordPrompt(string prompt)
    {
        var p = prompt.Trim();
        return p.EndsWith("password:", StringComparison.OrdinalIgnoreCase)
            && !NotTheLoginPassword.Any(w => p.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Host-key confirmations are the only prompts whose answer should echo.</summary>
    public static bool IsYesNoPrompt(string prompt) =>
        prompt.Contains("(yes/no", StringComparison.OrdinalIgnoreCase);
}
