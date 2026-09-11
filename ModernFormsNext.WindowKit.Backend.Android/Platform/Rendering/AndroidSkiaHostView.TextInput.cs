using Android.Views.InputMethods;
using ModernFormsNext.WindowKit.Input;
using InputTypes = Android.Text.InputTypes;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

public sealed partial class AndroidSkiaHostView
{
    private ITextInputClient? textInputClient;
    private bool textInputConfigured;
    private int nativeInputDepth;
    private TextInputOptions? lastTextInputOptions;
    private long lastTextInputRevision = -1;
    private long textInputGeneration;

    /// <summary>Offers one borrowed framework text session to Android, or retires native text ownership.</summary>
    /// <param name="client">The host-coordinate, revocable client; null disables native text input.</param>
    /// <remarks>
    /// Call on the Android main thread. After the first call, legacy text events and the legacy
    /// state provider no longer edit documents, including when client is null. Existing hosts that
    /// never call this method retain their event transport. The document and control remain borrowed.
    /// </remarks>
    public void SetClient(ITextInputClient? client)
    {
        VerifyTextInputThread();
        ThrowIfDisposed();
        if (textInputConfigured && ReferenceEquals(textInputClient, client)) return;
        var generation = ++textInputGeneration;
        textInputConfigured = true;
        DetachTextInputClient();
        if (generation != textInputGeneration || disposed) return;
        textInputClient = client;
        if (client is not null)
            client.StateChanged += OnTextClientStateChanged;
        var snapshot = client?.GetState(0);
        // Custom client subscriptions/queries can synchronously redirect framework focus.
        // The newest offer owns the native connection and keyboard, even inside this call.
        if (generation != textInputGeneration || !ReferenceEquals(client, textInputClient) || disposed) return;
        lastTextInputOptions = snapshot?.Options;
        lastTextInputRevision = snapshot?.Revision ?? -1;
        GetInputMethodManager()?.RestartInput(this);
        if (generation != textInputGeneration || disposed) return;
        if (snapshot is null || snapshot.Options.ReadOnly)
            GetInputMethodManager()?.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
        NotifyClientStateChanged();
    }

    /// <summary>Requests the software keyboard for the current editable framework session.</summary>
    /// <param name="visible">True to show; false to hide.</param>
    /// <returns>Whether Android accepted the request; keyboard visibility remains operating-system policy.</returns>
    /// <remarks>Call on the Android main thread. Showing never revives a retired or read-only client.</remarks>
    public bool SetKeyboardVisible(bool visible)
    {
        VerifyTextInputThread();
        ThrowIfDisposed();
        var manager = GetInputMethodManager();
        if (manager is null) return false;
        if (!visible)
            return manager.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
        var generation = textInputGeneration;
        if (textInputConfigured && textInputClient?.GetState(0) is not { Options.ReadOnly: false }) return false;
        if (generation != textInputGeneration || disposed) return false;
        RequestFocus();
        NotifyTextStateChanged();
        if (generation != textInputGeneration || disposed) return false;
        return manager.ShowSoftInput(this, ShowFlags.Implicit);
    }

    private static void VerifyTextInputThread()
    {
        if (global::Android.OS.Looper.MyLooper() != global::Android.OS.Looper.MainLooper)
            throw new InvalidOperationException("Android text input requires the main UI thread.");
    }

    private void DetachTextInputClient()
    {
        var previous = textInputClient;
        textInputClient = null;
        lastTextInputOptions = null;
        lastTextInputRevision = -1;
        RetireInputConnection();
        if (previous is not null) previous.StateChanged -= OnTextClientStateChanged;
    }

    private void RetireInputConnection()
    {
        var previous = activeInputConnection;
        activeInputConnection = null;
        inputStateNotificationPending = false;
        previous?.Revoke();
    }

    private void OnTextClientStateChanged(object? sender, EventArgs e)
        => NotifyClientStateChanged();

    private void NotifyClientStateChanged()
    {
        if (disposed || !textInputConfigured) return;
        if (nativeInputDepth > 0 || activeInputConnection?.BatchDepth > 0)
        {
            inputStateNotificationPending = true;
            return;
        }
        var current = textInputClient;
        var snapshot = current?.GetState(0);
        if (!ReferenceEquals(current, textInputClient)) return;
        if (snapshot is null)
        {
            RetireInputConnection();
            return;
        }
        inputStateNotificationPending = false;
        var manager = GetInputMethodManager();
        // Native edits update these tokens before their deferred state event is flushed. Changes
        // originating outside the IME restart the Android connection; geometry-only changes do not.
        if (lastTextInputOptions != snapshot.Options || lastTextInputRevision != snapshot.Revision)
        {
            RetireInputConnection();
            lastTextInputOptions = snapshot.Options;
            lastTextInputRevision = snapshot.Revision;
            manager?.RestartInput(this);
            if (!ReferenceEquals(current, textInputClient) || disposed) return;
            if (snapshot.Options.ReadOnly)
                manager?.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
        }
        manager?.UpdateSelection(this, snapshot.SelectionStart, snapshot.SelectionEnd,
            snapshot.CompositionStart, snapshot.CompositionEnd);
        activeInputConnection?.PublishMonitoredState();
    }

    private IInputConnection? CreateClientInputConnection(EditorInfo attributes)
    {
        var client = textInputClient;
        var snapshot = client?.GetState();
        if (snapshot is null || snapshot.Options.ReadOnly || !ReferenceEquals(client, textInputClient)) return null;
        var options = snapshot.Options;
        attributes.InputType = options.Scope switch
        {
            TextInputScope.Numeric => InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned,
            TextInputScope.Phone => InputTypes.ClassPhone,
            TextInputScope.Email => InputTypes.ClassText | InputTypes.TextVariationEmailAddress,
            TextInputScope.Url => InputTypes.ClassText | InputTypes.TextVariationUri,
            TextInputScope.Password => InputTypes.ClassText | InputTypes.TextVariationPassword | InputTypes.TextFlagNoSuggestions,
            _ => InputTypes.ClassText
        };
        if (options.MultiLine && options.Scope is not (TextInputScope.Numeric or TextInputScope.Phone))
            attributes.InputType |= InputTypes.TextFlagMultiLine;
        if (options.Scope is not (TextInputScope.Numeric or TextInputScope.Phone or TextInputScope.Password))
        {
            attributes.InputType |= options.AutoCorrect ? InputTypes.TextFlagAutoCorrect : InputTypes.TextFlagNoSuggestions;
            attributes.InputType |= options.Capitalization switch
            {
                TextInputCapitalization.Characters => InputTypes.TextFlagCapCharacters,
                TextInputCapitalization.Words => InputTypes.TextFlagCapWords,
                TextInputCapitalization.Sentences => InputTypes.TextFlagCapSentences,
                _ => 0
            };
        }
        var action = options.Action switch
        {
            TextInputAction.Done => ImeAction.Done,
            TextInputAction.Go => ImeAction.Go,
            TextInputAction.Search => ImeAction.Search,
            TextInputAction.Send => ImeAction.Send,
            TextInputAction.Next => ImeAction.Next,
            TextInputAction.Previous => ImeAction.Previous,
            _ => options.MultiLine ? ImeAction.None : ImeAction.Unspecified
        };
        attributes.ImeOptions = ImeFlags.NoExtractUi | (ImeFlags)(int)action;
        if (options.Scope == TextInputScope.Password && OperatingSystem.IsAndroidVersionAtLeast(26))
            attributes.ImeOptions |= ImeFlags.NoPersonalizedLearning;
        attributes.InitialSelStart = snapshot.SelectionStart;
        attributes.InitialSelEnd = snapshot.SelectionEnd;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            attributes.SetInitialSurroundingSubText(options.Scope == TextInputScope.Password ? string.Empty : snapshot.Text,
                options.Scope == TextInputScope.Password ? 0 : snapshot.TextStart);
        lastTextInputOptions = options;
        lastTextInputRevision = snapshot.Revision;
        activeInputConnection = new SharedInputConnection(this, client);
        return activeInputConnection;
    }
}
