using ModernFormsNext;

namespace ControlGallery.Examples;

/// <summary>Hosts the optional page composition authored in the companion .mfdesign document.</summary>
/// <remarks>
/// The UserControl owns its generated PageHost and retained pages. Construct it on the UI thread;
/// normal hosting starts presentation. Application routes and callbacks belong in user code.
/// This optional Gallery example is independent of the minimal default project template.
/// </remarks>
public sealed partial class DesignedPagesView : UserControl
{
    /// <summary>Creates the detached authored tree without starting a separate application runtime.</summary>
    public DesignedPagesView() => InitializeComponent();
}
