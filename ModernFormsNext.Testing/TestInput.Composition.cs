using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.Testing;

public sealed partial class TestInput
{
    /// <summary>Gets the production text-service session for the current hosted focus owner.</summary>
    /// <remarks>
    /// Retain this borrowed instance to test late callbacks after focus/host retirement. It reports
    /// logical window-client caret coordinates and rejects edits after retirement. This does not
    /// simulate an OS IME, keyboard layout or candidate UI.
    /// </remarks>
    public ITextInputClient? TextInputClient
    {
        get { VerifyAccess(); return window.CanReceiveInput ? window.HostedWindow.TextInputClient : null; }
    }

    /// <summary>Starts or updates composition through the real focused editor's shared client.</summary>
    /// <param name="text">Complete provisional Unicode text.</param>
    /// <param name="newCursorPosition">Positive offsets are relative to insertion end minus one; others to start.</param>
    /// <returns>Whether the production client accepted the edit.</returns>
    public bool SetComposingText(string text, int newCursorPosition = 1)
        => SendComposition("Compose", client => client.SetComposingText(text, newCursorPosition));

    /// <summary>Commits the current composition or selection through the semantic text-service path.</summary>
    /// <param name="text">Complete committed text, including an empty replacement.</param>
    /// <param name="newCursorPosition">Positive offsets are relative to insertion end minus one; others to start.</param>
    /// <returns>Whether the production client accepted the edit.</returns>
    /// <remarks>TextInput remains the existing raw-text helper; this operation supports native replacement semantics.</remarks>
    public bool CommitComposition(string text, int newCursorPosition = 1)
        => SendComposition("Commit", client => client.CommitText(text, newCursorPosition));

    /// <summary>Accepts visible provisional text through the production composition layer.</summary>
    /// <returns>Whether the production client accepted the operation.</returns>
    public bool FinishComposition() => SendComposition("Finish", client => client.FinishComposition());

    /// <summary>Restores the current composition checkpoint through the actual editor.</summary>
    /// <returns>Whether the production client accepted the operation.</returns>
    public bool CancelComposition() => SendComposition("Cancel", client => client.CancelComposition());

    private bool SendComposition(string kind, Func<ITextInputClient, bool> action)
    {
        VerifyAccess();
        if (TextInputClient is not { } client) return false;
        if (recentEvents.Count == HistoryLimit) recentEvents.Dequeue();
        recentEvents.Enqueue(kind);
        return action(client);
    }
}
