using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SimpleSshLean.Sshl;

/// <summary>
/// Talks to the console directly (CONIN$/CONOUT$). In askpass mode stdout is a
/// pipe back to ssh, so prompts must bypass it.
/// </summary>
internal static class ConsoleIO
{
    /// <summary>Shows the prompt and reads one line, or returns null when there is no console or input ends.</summary>
    public static string? Prompt(string prompt, bool echo)
    {
        using var output = Open("CONOUT$");
        using var input = Open("CONIN$");
        if (output.IsInvalid || input.IsInvalid || !GetConsoleMode(input, out uint original)) return null;

        Write(output, prompt);
        uint mode = (original | ENABLE_LINE_INPUT | ENABLE_PROCESSED_INPUT) & ~ENABLE_VIRTUAL_TERMINAL_INPUT;
        mode = echo ? mode | ENABLE_ECHO_INPUT : mode & ~ENABLE_ECHO_INPUT;
        SetConsoleMode(input, mode);
        try
        {
            return ReadLine(input);
        }
        finally
        {
            SetConsoleMode(input, original);
            if (!echo) Write(output, "\n");
        }
    }

    public static void Notice(string text)
    {
        using var output = Open("CONOUT$");
        if (!output.IsInvalid) Write(output, text + "\n");
    }

    static string? ReadLine(SafeFileHandle input)
    {
        var line = new StringBuilder();
        var buffer = new char[256];
        try
        {
            while (true)
            {
                if (!ReadConsoleW(input, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero) || read == 0)
                    return null; // Ctrl+C / Ctrl+Break, or the console went away
                line.Append(buffer, 0, (int)read);
                var text = line.ToString();
                int newline = text.IndexOf('\n');
                if (newline >= 0) return text[..newline].TrimEnd('\r');
            }
        }
        finally
        {
            Array.Clear(buffer);
            line.Clear();
        }
    }

    static void Write(SafeFileHandle output, string text)
    {
        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        WriteConsoleW(output, text, (uint)text.Length, out _, IntPtr.Zero);
    }

    static SafeFileHandle Open(string name) =>
        CreateFileW(name, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
    const uint ENABLE_PROCESSED_INPUT = 0x1, ENABLE_LINE_INPUT = 0x2, ENABLE_ECHO_INPUT = 0x4;
    const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x200;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(SafeFileHandle handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(SafeFileHandle handle, uint mode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ReadConsoleW(SafeFileHandle handle, [Out] char[] buffer, uint toRead, out uint read, IntPtr control);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool WriteConsoleW(SafeFileHandle handle, string buffer, uint toWrite, out uint written, IntPtr reserved);
}
