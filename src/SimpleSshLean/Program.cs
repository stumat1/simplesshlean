using SimpleSshLean;
using SimpleSshLean.Core;

ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001 // dark mode is still marked experimental in WinForms
Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

bool firstRun = !File.Exists(AppPaths.HostsFile);
var store = new HostStore(AppPaths.HostsFile);
string? message = store.Warning;

if (firstRun && store.Warning is null)
{
    try
    {
        int imported = LegacyImport.Run(store, AppPaths.LegacyHostsFile);
        if (imported > 0) message = $"Imported {imported} host(s) and their saved passwords from simplessh.";
    }
    catch (Exception e)
    {
        message = $"Could not import hosts from simplessh: {e.Message}";
    }
}

Application.Run(new MainForm(store, message));
