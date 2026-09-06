using System.Text.Json;
using System.Windows.Automation;

if (args.Length != 1 || !long.TryParse(args[0], out long rawHandle) || rawHandle == 0)
    return 2;

AutomationElement root = AutomationElement.FromHandle(new IntPtr(rawHandle));
AutomationElement? button = root.FindFirst(
    TreeScope.Descendants,
    new PropertyCondition(AutomationElement.AutomationIdProperty, "uia.integration.invoke"));

if (button is null)
{
    Console.Error.WriteLine($"Root: name='{root.Current.Name}', frameworkId='{root.Current.FrameworkId}', controlType={root.Current.ControlType.Id}");
    AutomationElement? candidate = TreeWalker.RawViewWalker.GetFirstChild(root);
    while (candidate is not null)
    {
        Console.Error.WriteLine(
            $"Child: name='{candidate.Current.Name}', automationId='{candidate.Current.AutomationId}', controlType={candidate.Current.ControlType.Id}");
        candidate = TreeWalker.RawViewWalker.GetNextSibling(candidate);
    }

    return 3;
}

string rootNameBeforeInvoke = root.Current.Name;
AutomationElement? disabledButton = root.FindFirst(
    TreeScope.Descendants,
    new PropertyCondition(AutomationElement.AutomationIdProperty, "uia.integration.disabled-command"));
if (disabledButton is null)
    return 5;
string buttonName = button.Current.Name;
string automationId = button.Current.AutomationId;
int rootControlType = root.Current.ControlType.Id;
int buttonControlType = button.Current.ControlType.Id;
object pattern = button.GetCurrentPattern(InvokePattern.Pattern);
if (pattern is not InvokePattern invokePattern)
    return 4;

invokePattern.Invoke();
button.SetFocus();

var result = new
{
    RootNameBeforeInvoke = rootNameBeforeInvoke,
    RootNameAfterInvoke = root.Current.Name,
    ButtonName = buttonName,
    AutomationId = automationId,
    RootControlType = rootControlType,
    ButtonControlType = buttonControlType,
    DisabledCommandIsEnabled = disabledButton.Current.IsEnabled,
    HasKeyboardFocus = button.Current.HasKeyboardFocus
};

Console.WriteLine(JsonSerializer.Serialize(result));
return 0;
