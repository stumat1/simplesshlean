using System.Text.Json.Serialization;

namespace SimpleSshLean.Core;

/// <summary>A saved host. Passwords are never stored here — see <see cref="CredentialStore"/>.</summary>
public sealed record HostEntry(string Id, string Name, string Host, int Port, string Username)
{
    [JsonIgnore]
    public SshTarget Target => new(Host, Port, Username);

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Target.Display : Name;
}
