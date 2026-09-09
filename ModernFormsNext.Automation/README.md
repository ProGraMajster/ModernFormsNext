# ModernFormsNext.Automation

Optional development-time **in-process semantic core**, issue #97 Phase 1a. It consumes the
canonical `AccessibleObject` tree and production UI dispatcher. No Testing dependency, listener,
IPC, discovery, authentication, MCP, screenshot, input simulation, or wait service is included.

Create a session on the initialized application UI thread, explicitly register allowed windows or
Skia surfaces, and await its semantic query/action methods. Dispose registrations/session before
the UI dispatcher stops. The session borrows roots; it does not close application windows.

See [the semantic core guide](https://github.com/ProGraMajster/ModernFormsNext/blob/master/docs/automation.md)
for lifetime, privacy, query completeness, action acceptance and Phase 1b boundaries.
