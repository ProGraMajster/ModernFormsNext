namespace ModernFormsNext.WindowKit.Input;

/// <summary>Describes a preferred software keyboard; it does not validate entered values.</summary>
public enum TextInputScope
{
    /// <summary>Ordinary text.</summary>
    Text,
    /// <summary>Numeric input.</summary>
    Numeric,
    /// <summary>Email address input.</summary>
    Email,
    /// <summary>Web address input.</summary>
    Url,
    /// <summary>Phone number input.</summary>
    Phone,
    /// <summary>Sensitive text with prediction and learning disabled where supported.</summary>
    Password
}

/// <summary>Describes an optional native keyboard capitalization hint.</summary>
public enum TextInputCapitalization
{
    /// <summary>No automatic capitalization requested.</summary>
    None,
    /// <summary>Capitalize sentence starts.</summary>
    Sentences,
    /// <summary>Capitalize word starts.</summary>
    Words,
    /// <summary>Capitalize all characters.</summary>
    Characters
}

/// <summary>Describes the semantic action displayed by a software keyboard's return key.</summary>
public enum TextInputAction
{
    /// <summary>Use ordinary editor return behavior.</summary>
    Default,
    /// <summary>Finish editing and request keyboard dismissal.</summary>
    Done,
    /// <summary>Navigate or submit.</summary>
    Go,
    /// <summary>Invoke search.</summary>
    Search,
    /// <summary>Send the editor's value.</summary>
    Send,
    /// <summary>Move using the framework's normal forward focus traversal.</summary>
    Next,
    /// <summary>Move using the framework's normal backward focus traversal.</summary>
    Previous
}

/// <summary>Contains immutable native text-service hints and the editor's current capabilities.</summary>
/// <remarks>
/// Hints do not constrain the document or guarantee platform support. ReadOnly does not prohibit
/// selection queries. Password scope overrides correction, capitalization and native learning.
/// Existing TextBox ReadOnly, MultiLine and PasswordCharacter remain authoritative defaults.
/// </remarks>
public sealed record TextInputOptions
{
    /// <summary>Gets the requested keyboard scope.</summary>
    public TextInputScope Scope { get; init; }
    /// <summary>Gets whether text modification is unavailable.</summary>
    public bool ReadOnly { get; init; }
    /// <summary>Gets whether the editor accepts multiple lines.</summary>
    public bool MultiLine { get; init; }
    /// <summary>Gets whether native correction is requested, subject to password privacy.</summary>
    public bool AutoCorrect { get; init; } = true;
    /// <summary>Gets the requested capitalization behavior.</summary>
    public TextInputCapitalization Capitalization { get; init; }
    /// <summary>Gets the requested return-key action.</summary>
    public TextInputAction Action { get; init; }
}
