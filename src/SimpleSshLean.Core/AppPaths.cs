namespace SimpleSshLean.Core;

public static class AppPaths
{
    static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string DataDir => Path.Combine(AppData, "SimpleSshLean");

    public static string HostsFile => Path.Combine(DataDir, "hosts.json");

    /// <summary>simplessh's host list, imported on first run.</summary>
    public static string SimplesshHostsFile => Path.Combine(AppData, "com.simplessh.v2", "hosts.json");

    /// <summary>
    /// Windows' bundled OpenSSH. Preferred over whatever ssh is first on PATH:
    /// other builds (e.g. Git for Windows' MSYS ssh) treat the console and
    /// SSH_ASKPASS differently.
    /// </summary>
    public static string SshExe => SystemOpenSsh("ssh.exe");

    public static string SshKeygenExe => SystemOpenSsh("ssh-keygen.exe");

    static string SystemOpenSsh(string exe)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", exe);
        return File.Exists(path) ? path : exe;
    }
}
