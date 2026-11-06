using System.Text.Json;

namespace SimpleSshLean.Core;

/// <summary>
/// The saved-host list as a versioned JSON file. Writes are atomic, a corrupt
/// file is moved aside instead of being overwritten, and a file from a newer
/// version is opened read-only so it can't be downgraded by accident.
/// </summary>
public sealed class HostStore
{
    public const int SchemaVersion = 1;

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    readonly List<HostEntry> hosts;

    public string FilePath { get; }

    /// <summary>Set when the file couldn't be loaded cleanly; worth showing the user.</summary>
    public string? Warning { get; }

    public bool IsReadOnly { get; }

    public HostStore(string filePath)
    {
        FilePath = filePath;
        (hosts, Warning, IsReadOnly) = Load(filePath);
    }

    public IReadOnlyList<HostEntry> Hosts => hosts;

    /// <summary>Insert or update by id (an empty id gets a new one). Returns the stored entry.</summary>
    public HostEntry Save(HostEntry entry)
    {
        var stored = string.IsNullOrEmpty(entry.Id) ? entry with { Id = Guid.NewGuid().ToString() } : entry;
        var next = new List<HostEntry>(hosts);
        int index = next.FindIndex(h => h.Id == stored.Id);
        if (index >= 0) next[index] = stored;
        else next.Add(stored);
        Commit(next);
        return stored;
    }

    public void Delete(string id) => Commit(hosts.Where(h => h.Id != id).ToList());

    void Commit(List<HostEntry> next)
    {
        if (IsReadOnly) throw new InvalidOperationException(Warning ?? "The host list is read-only.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new StoreFile(SchemaVersion, next), Json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }
            throw;
        }
        hosts.Clear();
        hosts.AddRange(next);
    }

    sealed record StoreFile(int Version, List<HostEntry>? Hosts);

    static (List<HostEntry> Hosts, string? Warning, bool ReadOnly) Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return ([], null, false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Read-only, so a later save can't replace a file we never saw.
            return ([], $"{path} could not be read: {e.Message}", true);
        }

        StoreFile? file;
        try
        {
            file = JsonSerializer.Deserialize<StoreFile>(text, Json);
        }
        catch (JsonException e)
        {
            var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(path, backup);
                return ([], $"{path} was corrupt ({e.Message}); it was moved to {backup}.", false);
            }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            {
                return ([], $"{path} is corrupt ({e.Message}) and could not be moved aside: {moveError.Message}", true);
            }
        }

        var loaded = (file?.Hosts ?? [])
            .Where(h => h is { Id.Length: > 0 } && SshTarget.Validate(h.Host, h.Port, h.Username) is null)
            .Select(h => h with { Name = h.Name ?? "" })
            .ToList();

        if (file is not null && file.Version > SchemaVersion)
            return (loaded, $"{path} was written by a newer version of SimpleSSH Lean, so the host list is read-only.", true);
        return (loaded, null, false);
    }
}
