using SimpleSshLean.Core;
using SimpleSshLean.Sshl;

// One exe, two roles:
//  - launcher: `sshl --host h --port p --user u` runs Windows' ssh.exe in this
//    console and offers a reconnect when the connection drops;
//  - askpass: ssh runs this exe again (via SSH_ASKPASS) whenever it needs to
//    ask something, and the launcher's environment marks that call.
var session = Environment.GetEnvironmentVariable(AskpassContract.SessionVar);
return string.IsNullOrEmpty(session) ? Launcher.Run(args) : Askpass.Run(args, session);
