using System.Globalization;

namespace SimpleSshLean.Core;

/// <summary>user@host:port — the identity a connection and its saved password are keyed by.</summary>
public readonly record struct SshTarget(string Host, int Port, string Username)
{
    public const int DefaultPort = 22;

    public string Display =>
        Port == DefaultPort ? $"{Username}@{Host}" : $"{Username}@{BracketIPv6(Host)}:{Port}";

    /// <summary>The name known_hosts and <c>ssh-keygen -R</c> use for this host.</summary>
    public string KnownHostsName => Port == DefaultPort ? Host : $"[{Host}]:{Port}";

    static string BracketIPv6(string host) => host.Contains(':') ? $"[{host}]" : host;

    /// <summary>Returns an error message, or null when the fields are usable.</summary>
    /// <remarks>
    /// Values end up as ssh arguments and in a Windows Terminal command line, so
    /// anything that could be read as an option or a command separator is refused.
    /// </remarks>
    public static string? Validate(string? host, int port, string? username)
    {
        if (string.IsNullOrWhiteSpace(host)) return "Enter a host name or IP address.";
        if (host.StartsWith('-') || !host.All(IsHostChar))
            return "The host may contain only letters, digits, '.', '-', '_' and ':' (IPv6).";
        if (port is < 1 or > 65535) return "The port must be between 1 and 65535.";
        if (string.IsNullOrWhiteSpace(username)) return "Enter a user name.";
        if (username.StartsWith('-') || username.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is ';' or '"'))
            return "The user name can't contain spaces, quotes or ';', or start with '-'.";
        return null;
    }

    static bool IsHostChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ':' or '%';

    /// <summary>Parses <c>user@host</c>, <c>user@host:port</c> or <c>user@[v6addr]:port</c>.</summary>
    public static bool TryParse(string? text, out SshTarget target, out string error)
    {
        target = default;
        text = text?.Trim() ?? "";
        int at = text.LastIndexOf('@');
        if (at <= 0 || at == text.Length - 1)
        {
            error = "Enter it as user@host or user@host:port.";
            return false;
        }

        string user = text[..at], rest = text[(at + 1)..], host = rest, portText = "";
        if (rest.StartsWith('['))
        {
            int close = rest.IndexOf(']');
            string tail = close < 0 ? "" : rest[(close + 1)..];
            if (close < 0 || (tail.Length > 0 && !tail.StartsWith(':')))
            {
                error = "Write an IPv6 address with a port as user@[address]:port.";
                return false;
            }
            host = rest[1..close];
            portText = tail.TrimStart(':');
        }
        else if (rest.Count(c => c == ':') == 1)
        {
            int colon = rest.IndexOf(':');
            host = rest[..colon];
            portText = rest[(colon + 1)..];
        }

        int port = DefaultPort;
        if (portText.Length > 0 && !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port))
        {
            error = "The port must be a number.";
            return false;
        }

        if (Validate(host, port, user) is { } problem)
        {
            error = problem;
            return false;
        }
        target = new SshTarget(host, port, user);
        error = "";
        return true;
    }
}
