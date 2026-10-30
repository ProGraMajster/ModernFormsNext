using ModernFormsNext;

// A real publish consumer of the complete assembly, rather than copied resource source files.
// No window or input automation is required; Android's native scenario is qualified separately.
var property = new ResourceProperty<ResourceProbe, int>(nameof(ResourceProbe.Number),
    static control => control.Number, static (control, value) => control.Number = value);
using var control = new InheritedResourceProbe { Number = 3 };
var shared = new ResourceDictionary();
var overrides = new ResourceDictionary();
control.Resources.MergedDictionaries.Add(shared);
control.Resources.MergedDictionaries.Add(overrides);
control.SetResourceReference(property, "Number");
Check(control.Number == 3, "missing-key fallback");
int calls = 0;
shared.SetFactory("Number", () => { calls++; return 8; });
Check(calls == 1 && control.Number == 8, "factory publication");
overrides["Number"] = 13;
Check(control.Number == 13, "last-merge precedence");
shared["Number"] = 21;
Check(control.Number == 13, "hidden mutation");
control.Resources.MergedDictionaries.Remove(overrides);
Check(control.Number == 21, "merge removal");
control.Resources["Number"] = 34;
Check(control.Number == 34, "local override");
control.Resources.Remove("Number");
Check(control.Number == 21, "local removal");
Check(control.ClearResourceReference(property.Name) && control.Number == 3, "captured fallback");

var serializer = new ThemeJsonSerializer();
var definition = new ThemeDefinition("resource.publish", "Resource publish") { Variant = ThemeVariant.Custom };
definition.Resources["Number"] = ThemeResourceValue.FromInteger(55);
ThemeDefinition roundTrip = serializer.Deserialize(serializer.Serialize(definition));
Check(roundTrip.Resources["Number"].Value is int number && number == 55, "closed theme serialization");
shared["Number"] = roundTrip.Resources["Number"].Value;
control.SetResourceReference(property, "Number");
Check(control.Number == 55, "serialized replacement");

#if LEGACY_RESOURCE_REFERENCE
// This opt-in negative publish must emit IL2026 with the typed-overload remediation.
// It is never used to claim a successful metadata-free deployment.
control.SetResourceReference(nameof(ResourceProbe.Number), "Number");
#endif

Console.WriteLine("DYNAMIC_RESOURCE_PUBLISH_PASS: 10 assertions; typed inherited property, merges, factory, fallback, serialization, replacement.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Dynamic resource publish failed: {message}.");
}

internal class ResourceProbe : Control
{
    public int Number { get; set; }
}
internal sealed class InheritedResourceProbe : ResourceProbe;
