using System.Text.Json;

namespace SimpleSshLean.Core;

/// <summary>One-time import of hosts and saved passwords from the original simplessh app.</summary>
public static class LegacyImport
{
    /// <summary>Returns the number of hosts imported.</summary>
    public static int Run(HostStore store, string legacyHostsFile)
    {
        if (!File.Exists(legacyHostsFile)) return 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(legacyHostsFile));
        if (!doc.RootElement.TryGetProperty("hosts", out var list) || list.ValueKind != JsonValueKind.Array)
            return 0;

        int count = 0;
        foreach (var item in list.EnumerateArray())
        {
            string host = Text(item, "host"), user = Text(item, "username");
            int port = item.TryGetProperty("port", out var p) && p.TryGetInt32(out var n) ? n : SshTarget.DefaultPort;
            if (SshTarget.Validate(host, port, user) is not null) continue;

            var entry = store.Save(new HostEntry("", Text(item, "name"), host, port, user));
            count++;

            var target = CredentialStore.TargetFor(entry.Target);
            if (!CredentialStore.Exists(target) && CredentialStore.Read(CredentialStore.LegacyTargetFor(entry.Target)) is { } password)
                CredentialStore.Write(target, user, password);
        }
        return count;
    }

    static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
