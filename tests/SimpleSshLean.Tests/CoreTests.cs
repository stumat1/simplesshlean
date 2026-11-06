using SimpleSshLean.Core;

namespace SimpleSshLean.Tests;

public class SshTargetTests
{
    [Theory]
    [InlineData("alice@nas", "nas", 22, "alice")]
    [InlineData(" alice@192.168.0.19:2222 ", "192.168.0.19", 2222, "alice")]
    [InlineData("bob@[fe80::1]:2200", "fe80::1", 2200, "bob")]
    [InlineData("bob@fe80::1", "fe80::1", 22, "bob")]
    [InlineData("ad@corp@host", "host", 22, "ad@corp")]
    public void Parses(string text, string host, int port, string user)
    {
        Assert.True(SshTarget.TryParse(text, out var target, out var error), error);
        Assert.Equal(new SshTarget(host, port, user), target);
    }

    [Theory]
    [InlineData("nas")]
    [InlineData("@nas")]
    [InlineData("alice@")]
    [InlineData("alice@nas:port")]
    [InlineData("alice@nas:70000")]
    [InlineData("alice@-oProxyCommand=calc")]
    [InlineData("-oProxyCommand=calc@nas")]
    [InlineData("alice@nas;calc")]
    [InlineData("al ice@nas")]
    [InlineData("bob@[fe80::1")]
    public void Rejects(string text) => Assert.False(SshTarget.TryParse(text, out _, out _));

    [Fact]
    public void KnownHostsNameBracketsNonDefaultPorts()
    {
        Assert.Equal("nas", new SshTarget("nas", 22, "a").KnownHostsName);
        Assert.Equal("[nas]:2222", new SshTarget("nas", 2222, "a").KnownHostsName);
    }
}

public class AskpassContractTests
{
    [Theory]
    [InlineData("alice@nas's password: ", true)]
    [InlineData("(alice@nas) Password: ", true)]
    [InlineData("Password:", true)]
    [InlineData("(current) UNIX password: ", true)]
    [InlineData("New password: ", false)]
    [InlineData("Retype new password: ", false)]
    [InlineData("Enter passphrase for key 'C:\\Users\\a\\.ssh\\id_ed25519': ", false)]
    [InlineData("Verification code: ", false)]
    [InlineData("Are you sure you want to continue connecting (yes/no/[fingerprint])? ", false)]
    public void ClassifiesPasswordPrompts(string prompt, bool expected) =>
        Assert.Equal(expected, AskpassContract.IsPasswordPrompt(prompt));

    [Fact]
    public void HostKeyPromptEchoes() =>
        Assert.True(AskpassContract.IsYesNoPrompt("Are you sure you want to continue connecting (yes/no/[fingerprint])? "));
}

/// <summary>Talks to the real Credential Manager with throwaway targets, removed afterwards.</summary>
public sealed class CredentialStoreTests : IDisposable
{
    readonly SshTarget target = new($"test-{Guid.NewGuid():N}.invalid", 2222, "tester");
    readonly string dir = Directory.CreateTempSubdirectory("simplesshlean-tests-").FullName;

    public void Dispose()
    {
        CredentialStore.Delete(CredentialStore.TargetFor(target));
        CredentialStore.Delete(CredentialStore.SimplesshTargetFor(target));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void WriteReadDeleteRoundTrip()
    {
        var key = CredentialStore.TargetFor(target);
        Assert.Null(CredentialStore.Read(key));
        CredentialStore.Write(key, target.Username, "pä$$wörd ✓");
        Assert.True(CredentialStore.Exists(key));
        Assert.Equal("pä$$wörd ✓", CredentialStore.Read(key));
        CredentialStore.Delete(key);
        Assert.False(CredentialStore.Exists(key));
        CredentialStore.Delete(key); // deleting a missing credential is not an error
    }

    [Fact]
    public void ImportsHostsAndPasswordsFromSimplessh()
    {
        CredentialStore.Write(CredentialStore.SimplesshTargetFor(target), target.Username, "from-simplessh");
        var simplessh = Path.Combine(dir, "simplessh-hosts.json");
        File.WriteAllText(simplessh, $$"""
            { "version": 1, "data": [
              { "id": "a", "name": "Lab", "host": "{{target.Host}}", "port": 2222, "username": "tester", "savePassword": true },
              { "id": "b", "name": "Bad", "host": "-oProxyCommand=x", "port": 22, "username": "tester", "savePassword": false }
            ] }
            """);
        var store = new HostStore(Path.Combine(dir, "hosts.json"));

        Assert.Equal(1, SimplesshImport.Run(store, simplessh));
        var host = Assert.Single(store.Hosts);
        Assert.Equal(("Lab", target), (host.Name, host.Target));
        Assert.Equal("from-simplessh", CredentialStore.Read(CredentialStore.TargetFor(target)));
    }

    [Fact]
    public void MissingSimplesshFileImportsNothing()
    {
        var store = new HostStore(Path.Combine(dir, "hosts.json"));
        Assert.Equal(0, SimplesshImport.Run(store, Path.Combine(dir, "nope.json")));
        Assert.Empty(store.Hosts);
    }

    [Theory]
    [InlineData("""{ "version": 2, "data": [] }""")]
    [InlineData("""{ "hosts": [] }""")]
    public void UnknownSimplesshFormatIsReportedNotGuessed(string json)
    {
        var simplessh = Path.Combine(dir, "simplessh-hosts.json");
        File.WriteAllText(simplessh, json);
        var store = new HostStore(Path.Combine(dir, "hosts.json"));
        Assert.Throws<InvalidDataException>(() => SimplesshImport.Run(store, simplessh));
        Assert.Empty(store.Hosts);
    }
}

public sealed class HostStoreTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("simplesshlean-tests-").FullName;
    string FilePath => Path.Combine(dir, "hosts.json");

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public void MissingFileIsEmpty()
    {
        var store = new HostStore(FilePath);
        Assert.Empty(store.Hosts);
        Assert.Null(store.Warning);
        Assert.False(store.IsReadOnly);
    }

    [Fact]
    public void SaveUpdateDeleteRoundTrip()
    {
        var store = new HostStore(FilePath);
        var added = store.Save(new HostEntry("", "NAS", "nas", 22, "alice"));
        Assert.False(string.IsNullOrEmpty(added.Id));
        store.Save(added with { Port = 2222 });
        store.Save(new HostEntry("", "", "pi", 22, "bob"));

        var reloaded = new HostStore(FilePath);
        Assert.Equal(2, reloaded.Hosts.Count);
        Assert.Equal(2222, reloaded.Hosts.Single(h => h.Id == added.Id).Port);
        Assert.Contains("\"version\": 1", File.ReadAllText(FilePath));

        reloaded.Delete(added.Id);
        Assert.Equal("pi", Assert.Single(new HostStore(FilePath).Hosts).Host);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void CorruptFileIsMovedAside()
    {
        File.WriteAllText(FilePath, "{ not json");
        var store = new HostStore(FilePath);
        Assert.Empty(store.Hosts);
        Assert.NotNull(store.Warning);
        Assert.False(File.Exists(FilePath));
        Assert.Single(Directory.GetFiles(dir, "hosts.json.corrupt-*"));
    }

    [Fact]
    public void NewerSchemaIsReadOnly()
    {
        File.WriteAllText(FilePath, """{ "version": 99, "hosts": [ { "id": "1", "name": "x", "host": "nas", "port": 22, "username": "a" } ] }""");
        var store = new HostStore(FilePath);
        Assert.Single(store.Hosts);
        Assert.True(store.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => store.Save(new HostEntry("", "", "pi", 22, "b")));
    }

    [Fact]
    public void InvalidEntriesAreDropped()
    {
        File.WriteAllText(FilePath, """{ "version": 1, "hosts": [ { "id": "1", "host": "-oProxyCommand=x", "port": 22, "username": "a" }, { "id": "2", "host": "nas", "port": 22, "username": "a" } ] }""");
        var host = Assert.Single(new HostStore(FilePath).Hosts);
        Assert.Equal("", host.Name);
    }
}
