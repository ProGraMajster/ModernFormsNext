using System.Windows.Input;

namespace ModernFormsNext.DataBinding;

// Shared by visual and non-visual action sources. CommandSource centralizes binding behavior;
// each source retains local Enabled intent instead of letting requery overwrite that intent.
internal interface ICommandBindingTargetProvider
{
    ICommand? Command { get; set; }
    object? CommandParameter { get; set; }
    bool Enabled { get; }
    bool IsCommandSourceDisposed { get; }
    void SetCommandEnabled(bool enabled);
}
