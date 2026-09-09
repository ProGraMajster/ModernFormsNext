using System.Drawing;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Automation;
using ModernFormsNext.Automation.Windows;
using ModernFormsNext.WindowKit.Threading;

// Dedicated real native fixture. Stdin is a test-only application control plane, never part of
// the bridge protocol. It releases deterministic async gates and controls process/root lifetime.
using var form = new Form { Text = "ModernFormsNext live bridge fixture", ClientSize = new Size(620, 500) };
using var secondary = new Form { Text = "Closable bridge root", ClientSize = new Size(220, 140) };
var status = new TextBox { AccessibleAutomationId = "status", Text = "Ready", ReadOnly = true, Bounds = new(20, 20, 500, 32) };
var input = new TextBox { AccessibleAutomationId = "input", Text = "Initial", Bounds = new(20, 60, 250, 32) };
var password = new TextBox { AccessibleAutomationId = "password-secret", Text = "PASSWORD-MARKER-97", PasswordCharacter = '*', Bounds = new(290, 60, 250, 32) };
var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
int invocations = 0;
var invoke = new Button { AccessibleAutomationId = "invoke", Text = "Invoke", Bounds = new(20, 105, 150, 36),
    Command = new DelegateCommand(() => status.Text = "Invoked:" + ++invocations) };
var asyncButton = new Button { AccessibleAutomationId = "async", Text = "Async", Bounds = new(190, 105, 150, 36),
    Command = new AsyncCommand(async () => { status.Text = "Pending"; await gate.Task; status.Text = "Completed"; }) };
form.Controls.Add(status); form.Controls.Add(input); form.Controls.Add(password); form.Controls.Add(invoke); form.Controls.Add(asyncButton);
form.Controls.Add(new CheckBox { AccessibleAutomationId = "check", Text = "Check", Bounds = new(20, 155, 150, 32) });
var list = new ListBox { AccessibleAutomationId = "list", Bounds = new(20, 200, 250, 150) }; list.Items.Add("One"); list.Items.Add("Two"); form.Controls.Add(list);
var tree = new TreeView { AccessibleAutomationId = "tree", Bounds = new(290, 200, 250, 150) }; tree.Items.Add(new TreeViewItem("Parent", new TreeViewItem("Child"))); form.Controls.Add(tree);
if (args.Contains("--privacy"))
{
    form.Controls.Add(new PrivateControl());
    form.Controls.Add(new FaultControl());
}
AutomationSession? semantic = null; AutomationRootRegistration? root = null; AutomationRootRegistration? otherRoot = null;
WindowsAutomationServer? server = null;
form.Shown += (_, _) =>
{
    try
    {
        semantic = new(); root = semantic.RegisterRoot(form);
        secondary.Show(); otherRoot = semantic.RegisterRoot(secondary);
        if (!args.Contains("--no-server")) server = WindowsAutomationServer.Start(semantic, new()
        {
            ApplicationName = "ModernFormsNext bridge test host",
            MaxResponseBytes = args.Contains("--small-response") ? 4096 : 4 * 1024 * 1024
        });
        Console.WriteLine("READY:" + JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, Application = server?.Application,
            RootId = root.RootId, OtherRootId = otherRoot.RootId,
            Password = new AutomationNodeHandle(semantic.SessionId, password.AccessibilityObject.RuntimeId.ToString())
        }));
        // Console's synchronized reader can block even through ReadLineAsync. Isolate this
        // fixture-only stdin loop from the UI thread, then dispatch each application command.
        _ = Task.Run(ReadCommands);
    }
    catch (Exception error)
    {
        Console.WriteLine("START-ERROR:" + (error is AutomationTransportException transport ? transport.Error.ToString() : error.GetType().Name));
        form.Close();
    }
};

async Task ReadCommands()
{
    while (await Console.In.ReadLineAsync() is { } command)
    {
        // All actual UI changes use the production dispatcher; this is application fixture code,
        // not an alternative automation action implementation.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (command == "complete") gate.TrySetResult();
            else if (command == "close-root") secondary.Close();
            else if (command == "remove-input") form.Controls.Remove(input);
            else if (command == "crash") Environment.Exit(97);
            else if (command == "stop-server") _ = StopServer();
            else if (command == "quit") _ = Quit();
        }).GetTask().ConfigureAwait(false);
    }
}
async Task StopServer()
{
    if (server is not null) await server.StopAsync();
    Console.WriteLine("SERVER-STOPPED:" + (semantic?.IsStopped == false));
}
async Task Quit()
{
    if (server is not null) await server.StopAsync();
    root?.Dispose(); otherRoot?.Dispose(); semantic?.Dispose(); secondary.Close(); form.Close();
}
Application.Run(form);

internal sealed class PrivateControl : Control
{
    protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
    private sealed class Peer(Control owner) : ControlAccessibleObject(owner)
    {
        public override bool IsSensitive => true;
        public override string? Name { get => "CUSTOM-GETTER-MARKER-97"; set { } }
        public override string? Value { get => "SENSITIVE-MARKER-97"; set { } }
    }
}
internal sealed class FaultControl : Control
{
    protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
    private sealed class Peer(Control owner) : ControlAccessibleObject(owner)
    {
        public override string? AutomationId => "fault";
        public override string? Name { get => throw new InvalidOperationException("EXCEPTION-MARKER-97"); set { } }
        public override AccessibleActions SupportedActions => AccessibleActions.SetValue;
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
            => throw new InvalidOperationException("ACTION-MARKER-97:" + parameter);
    }
}
