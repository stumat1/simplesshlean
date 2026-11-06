using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace SimpleSshLean.Core;

/// <summary>Saved passwords in the Windows Credential Manager (generic credentials, per user).</summary>
public static class CredentialStore
{
    public static string TargetFor(SshTarget t) => $"SimpleSshLean:{t.Username}@{t.Host}:{t.Port}";

    /// <summary>The target name the original simplessh app (Rust keyring crate) used.</summary>
    public static string LegacyTargetFor(SshTarget t) => $"pw:{t.Username}@{t.Host}:{t.Port}.simplessh";

    public static bool Exists(string target)
    {
        if (!CredReadW(target, CRED_TYPE_GENERIC, 0, out var ptr)) return false;
        CredFree(ptr);
        return true;
    }

    public static string? Read(string target)
    {
        if (!CredReadW(target, CRED_TYPE_GENERIC, 0, out var ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            int size = (int)cred.CredentialBlobSize;
            if (size == 0 || cred.CredentialBlob == IntPtr.Zero) return "";
            var bytes = new byte[size];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, size);
            try
            {
                // Windows convention (and the keyring crate) is UTF-16; tolerate UTF-8 from other writers.
                return size % 2 == 0 ? Encoding.Unicode.GetString(bytes) : Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public static void Write(string target, string userName, string secret)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        if (bytes.Length > CRED_MAX_CREDENTIAL_BLOB_SIZE)
            throw new ArgumentException("The password is too long for the Windows Credential Manager.");
        var blob = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = userName,
                Comment = "SimpleSSH Lean saved password",
                CredentialBlob = blob,
                CredentialBlobSize = (uint)bytes.Length,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
            };
            if (!CredWriteW(ref cred, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Array.Clear(bytes);
        }
    }

    public static void Delete(string target)
    {
        if (CredDeleteW(target, CRED_TYPE_GENERIC, 0)) return;
        int error = Marshal.GetLastWin32Error();
        if (error != ERROR_NOT_FOUND) throw new Win32Exception(error);
    }

    const uint CRED_TYPE_GENERIC = 1;
    const uint CRED_PERSIST_LOCAL_MACHINE = 2;
    const int CRED_MAX_CREDENTIAL_BLOB_SIZE = 5 * 512;
    const int ERROR_NOT_FOUND = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr buffer);
}
