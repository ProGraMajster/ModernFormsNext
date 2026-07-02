# Data Binding

ModernFormsNext 1.3.0 includes the first framework-level port of WinForms-style data
binding. The API is code-first and lives in the `ModernFormsNext.DataBinding` namespace.

The current implementation is intended to make ordinary one-way and two-way property
binding possible without depending on Windows Forms controls, designers, or binary
formatter infrastructure.

## Core Types

- `Binding` connects one bindable component property to one data source member.
- `ControlBindingsCollection` is exposed through `DataBindings` on bindable components.
- `BindingSource` wraps list-like data sources and provides current-item navigation,
  change notifications, sorting, filtering, and list mutation APIs where the source
  supports them.
- `BindingContext`, `CurrencyManager`, and `PropertyManager` coordinate current item
  state for controls that share the same data source.

These types intentionally follow familiar WinForms names where practical, while keeping
the implementation inside the ModernFormsNext framework layer.

## Basic Example

```csharp
using System.ComponentModel;
using ModernFormsNext;
using ModernFormsNext.DataBinding;

public sealed class Person : INotifyPropertyChanged
{
    private string name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
            {
                return;
            }

            name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
}

public sealed class MainForm : Form
{
    private readonly TextBox nameTextBox = new()
    {
        Width = 240
    };

    private readonly BindingSource people = new();

    public MainForm()
    {
        people.DataSource = new BindingList<Person>
        {
            new() { Name = "Ada" }
        };

        nameTextBox.DataBindings.Add(
            nameof(TextBox.Text),
            people,
            nameof(Person.Name),
            true,
            DataSourceUpdateMode.OnPropertyChanged);

        Controls.Add(nameTextBox);
    }
}
```

Use `DataSourceUpdateMode.OnPropertyChanged` when the data source should be updated as
the component property changes. Use `DataSourceUpdateMode.OnValidation` for the default
WinForms-like behavior where the data source is updated during validation. Use
`DataSourceUpdateMode.Never` for read-only bindings and call `Binding.WriteValue`
explicitly when an update is needed.

## Formatting and Conversion

Binding conversion is handled by ModernFormsNext, not by the obsolete binary formatter
path used by old Windows Forms internals.

Normal conversions prefer:

- `TypeConverter` implementations on the source or target type.
- `IConvertible` and culture-aware conversion.
- `IFormattable`, format strings, and `IFormatProvider`.
- `Binding.Format` and `Binding.Parse` event handlers for application-specific rules.

`System.Text.Json` is used only as a final fallback for object-to-string and
string-to-object cases where the normal .NET conversion pipeline does not provide a
converter. This keeps the binding path compatible with modern .NET trimming and avoids
binary serialization.

## Null Values

Use `Binding.NullValue` to choose what a null data-source value should display in the
bound component. Use `Binding.DataSourceNullValue` to choose the value written back to
the data source when the formatted component value represents null.

For nullable value types the default data-source null value is `null`. For non-nullable
types the default follows the WinForms-style `DBNull.Value` behavior.

## Current Limitations

- `BindingNavigator` is not compiled in 1.3.0 because the original WinForms version is
  based on `ToolStrip`. It needs a ModernFormsNext-native navigation control instead of
  a direct port.
- The ModernFormsNext designer is separate from the data-binding runtime. WinForms-only designer
  serialization hooks remain out of scope for this binding layer.
- Data binding is framework code and should be used from the UI thread, like the rest of
  the ModernFormsNext control model.
- Windows is currently the best-supported runtime target for the framework as a whole.
  Keep platform-specific behavior behind backend APIs when adding new binding scenarios.
