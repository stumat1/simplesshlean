# SimpleSSH Lean

A tiny saved-host manager for Windows that opens SSH sessions as **Windows
Terminal tabs**, using the **OpenSSH client built into Windows**. It doesn't
include an SSH stack or a terminal emulator of its own. The app only keeps your
host list and saved passwords, and Windows handles the rest.

It's the lean sibling of [simplessh](https://github.com/stumat1/simplessh) (Tauri + russh + xterm.js).

## How it works

```
SimpleSshLean.exe (WinForms host list)
   │  wt.exe -w 0 new-tab -- sshl.exe --host … --port … --user …
   ▼
Windows Terminal tab
   └─ sshl.exe (launcher)          runs ssh, offers "R to reconnect" when it drops
        └─ ssh.exe (C:\Windows\System32\OpenSSH)
             └─ sshl.exe (askpass)  SSH_ASKPASS=sshl.exe, SSH_ASKPASS_REQUIRE=force
                  ├─ login password prompt → saved password from Credential Manager (once)
                  └─ anything else         → asked right in the terminal tab
```

- **One helper exe, two roles.** `sshl.exe` is the per-tab launcher. ssh also
  runs it as its askpass program. The launcher marks the askpass calls with
  environment variables that hold only an attempt id and a Credential Manager
  target name, never a secret.
- **The saved password is sent at most once per attempt.** The launcher holds
  a named semaphore and askpass takes it before answering. If ssh asks for the
  password a second time, the saved one was rejected. The user then gets a
  normal prompt instead of ssh retrying a wrong password three times.
- **Everything else is plain OpenSSH.** Host-key checking, `~/.ssh/known_hosts`,
  `~/.ssh/config`, keys, `ssh-agent`, and keyboard-interactive/OTP prompts work
  exactly as with `ssh` on the command line.

## Features

- Saved hosts (name, host, port, user) plus quick connect (`user@host[:port]`).
- Saved passwords live in the Windows Credential Manager (`SimpleSshLean:user@host:port`)
  and are never written to disk by this app.
- Tabs, copy/paste, links, themes and fonts come from Windows Terminal. If
  Terminal isn't installed, a plain console window opens instead.
- Reconnect: when a session ends with an error, press **R** in the tab.
- "Forget trusted host key" (right-click) runs `ssh-keygen -R` after a known,
  legitimate host-key change.
- On first run, hosts and saved passwords from **simplessh** are imported, if
  it's installed. simplessh's data is only read, never changed. It isn't
  required: without it, the host list just starts empty.

Shortcuts: **Enter** connects, **F2** edits, **Del** deletes, **Ctrl+N** adds a host,
and **Ctrl+L** jumps to quick connect.

## Requirements

- Windows 10/11 with the **OpenSSH Client** optional feature (installed by default on Windows 11)
- [Windows Terminal](https://aka.ms/terminal) (recommended)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to run, or the .NET 10 SDK to build

## Build and run

```powershell
dotnet build                                      # everything
dotnet test                                       # unit + Credential Manager tests
dotnet run --project src/SimpleSshLean            # run the app
dotnet publish src/SimpleSshLean -c Release -o dist   # ~600 KB, framework-dependent
```

`dist\` holds `SimpleSshLean.exe` and `sshl.exe`, which must stay side by side.
You can also run `sshl.exe` directly:

```powershell
sshl --host nas --user alice [--port 2222] [--name "NAS"] [-- <extra ssh options>]
```

## Project layout

```
src/SimpleSshLean.Core/   host store (versioned JSON, atomic writes), Credential Manager,
                          target validation, askpass contract, simplessh import
src/sshl/                 console helper: launcher + askpass
src/SimpleSshLean/        WinForms host list and editor, Windows Terminal launch
tests/                    xUnit tests
```

## Data

- Hosts: `%APPDATA%\SimpleSshLean\hosts.json` (`{"version": 1, "hosts": [...]}`).
  A corrupt file is moved aside. A file written by a newer version opens read-only.
- Passwords: Windows Credential Manager, generic credentials `SimpleSshLean:*`.
- Trusted host keys: OpenSSH's own `%USERPROFILE%\.ssh\known_hosts`.

## Trade-offs compared with simplessh

- The terminal experience is Windows Terminal's. That's better in most ways
  (rendering, settings, panes), but it isn't contained in one app window.
- Passwords are saved when you enter them in the host editor, not after the
  first successful login.
- A changed host key is refused by OpenSSH with its standard warning, and the
  app has no "trust anyway" button. Use "Forget trusted host key" first.
- Saved passwords are only offered at prompts that look like a login password
  (`…password:`). Passphrase, OTP and password-change prompts always go to you.
