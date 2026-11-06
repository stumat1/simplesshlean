using System.Text.Json;

namespace SimpleSshLean.Core;

/// <summary>One-time import of hosts and saved passwords from simplessh (v2).</summary>
public static class SimplesshImport
{
    /// <summary>The <c>hosts.json</c> format version this import understands.</summary>
    const int SupportedVersion = 1;

    /// <summary>Returns the number of hosts imported. Throws when the file can't be used.</summary>
    /// <remarks>
    /// simplessh writes <c>{ "version": 1, "data": [ { "id", "name", "host", "port",
    /// "username", "savePassword" } ] }</c>. Its files are only read, never changed.
    /// </remarks>
    public static int Run(HostStore store, string simplesshHostsFile)
    {
        if (!File.Exists(simplesshHostsFile)) return 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(simplesshHostsFile));
        var root = doc.RootElement;
        int version = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("version", out var v) && v.TryGetInt32(out var n)
            ? n
            : 0;
        if (version != SupportedVersion)
            throw new InvalidDataException($"{simplesshHostsFile} uses format {version}, which this version can't read.");
        if (!root.TryGetProperty("data", out var list) || list.ValueKind != JsonValueKind.Array)
            return 0;

        int count = 0;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string host = Text(item, "host"), user = Text(item, "username");
            int port = item.TryGetProperty("port", out var p) && p.TryGetInt32(out var portNumber) ? portNumber : SshTarget.DefaultPort;
            if (SshTarget.Validate(host, port, user) is not null) continue;

            var entry = store.Save(new HostEntry("", Text(item, "name"), host, port, user));
            count++;

            var target = CredentialStore.TargetFor(entry.Target);
            if (!CredentialStore.Exists(target) && CredentialStore.Read(CredentialStore.SimplesshTargetFor(entry.Target)) is { } password)
                CredentialStore.Write(target, user, password);
        }
        return count;
    }

    static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
