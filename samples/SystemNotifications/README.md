# System notification sample

Dedicated Windows sample for issue #158; it does not change the default generated DemoApp.
See [the guide](../../docs/system-notifications.md) for runtime/installer prerequisites and API examples.

```powershell
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --smoke
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --classic --smoke
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --shell --smoke
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --leave-notification
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --history
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --leave-basic
dotnet run --project samples/SystemNotifications/SystemNotifications.csproj -- --show-basic
```

Default backend is explicitly App SDK so a broken modern installation cannot silently pass as
Shell. `--classic` uses legacy templates unless `--installed-classic` is also supplied and the
installer has registered AUMID `ProGraMajster.ModernFormsNext.NotificationSample`, a Start Menu
shortcut/ToastActivatorCLSID and LocalServer32 for CLSID `8C8F893B-7B43-48CC-90BF-68D02C319EF3`.
The sample does not install those classic keys/shortcuts. Do not point production registration
at a temporary worktree. Ordinary App SDK Register does its documented per-user registration.

The supplied thumbnail is the repository icon; choose a real local video thumbnail to check
cropping. Show Download, Update, Complete, Reply + choice, history and remove operations call
the real service. Open/Open folder buttons display the received action; this sample does not
download or execute files. A downloader must resolve its own persisted queue record first.

`--smoke` checks native acceptance/history/data updates/replacement/removal and exits. It includes
20 rapid progress updates and a subsequent lower value, verifying the retained native data. It also
verifies that ungrouped dismissal preserves another group with the same logical ID and that a logical group named mfn.default cannot alias the empty group. It does not certify banner rendering,
audio or focus. `--leave-notification` retains a reply/selection
notification then disposes/closes, allowing a real Notification Center cold-launch test.
It verifies that Windows history contains the reply before exiting. `--history` reads history in
a separate process without resending content. `--leave-basic` does the same for a plain message;
`--show-basic` sends that message and leaves the sample open. A retained history entry does not
prove that Windows rendered it. The Basic message button also exercises this minimal payload.

`Test-Activation.ps1` is a separate synthetic COM activation probe after registration. It
requires the sample to be closed, requests cold action activation with two input values, then
body activation in the running process and orderly shutdown. A failure must be investigated;
it must not be presented as a successful native click test. See the recorded validation status.
`Test-Activation.ps1 -StartProcessManually` separately tests the SDK's first-payload startup path:
it starts the registered EXE with the SDK COM launch marker, supplies a real native COM callback,
then verifies a subsequent warm callback. It bypasses Windows process launch and reports a
different success marker. The first SDK payload must be read with `AppInstance.GetActivatedEventArgs`
after registration; subscribing only to `NotificationInvoked` loses this startup activation.

The sample's Closing handler cancels the first close, awaits service disposal while the UI
dispatcher still runs, and then closes. Library disposal preserves native history and persistent
registration. Installer removal of the sample identity is a separate explicit action.

The stage-2 API uses `ModernFormsNext.Notifications`, semantic image roles, typed option
collections, logical Id/Group and backend-owned progress ordering. `--restart-seed` leaves
progress with long logical keys; a separate `--restart-finish` process recovers history, updates
numeric and indeterminate values, and removes it. Both stages inspect actual native data with a
bounded persistence wait. Run them consecutively under the same registration/executable path.
