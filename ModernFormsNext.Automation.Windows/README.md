# ModernFormsNext.Automation.Windows

Optional Windows development bridge over `ModernFormsNext.Automation`. Explicit server startup
enables local authenticated Named Pipes, per-user discovery and one active client. Merely referencing
this package creates no listener, file or credential, in either Debug or Release.

The bridge borrows an application's initialized semantic session. All live reads and actions remain
in that neutral core. No Testing runtime dependency, MCP, TCP, Android transport or private control
access is included. Same-user ACLs and tokens are not a sandbox against the same user or an administrator.

See [the live bridge guide](https://github.com/ProGraMajster/ModernFormsNext/blob/master/docs/automation-live-bridge.md)
for startup, security, client/CLI usage, bounded waits and limitations.
