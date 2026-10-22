using System.Globalization;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext;

public partial class AppShell
{
    /// <summary>Gets the maximum number of canonical visits in one exported route-history snapshot.</summary>
    public const int MaximumSavedRouteCount = 32;
    /// <summary>Gets the shell's route-only state schema version.</summary>
    public const int SavedStateVersion = 1;

    /// <summary>Copies canonical route history into the existing bounded application state handoff.</summary>
    /// <param name="stateKey">A caller-chosen identifier of at most 64 ASCII letters, digits, underscores or hyphens.</param>
    /// <returns>Immutable versioned string data; no storage or control serialization is performed.</returns>
    /// <remarks>
    /// UI thread only. The first character of the key must be a letter or underscore. Choose distinct
    /// keys for independent shells. This captures outer route visits, not arbitrary nested controls,
    /// editor contents, delegates, native handles or application objects. At most 32 visits and the
    /// existing StateData 64-entry/4096-value/65536-total UTF-16 limits are accepted. The application owns
    /// persistence. Route/parameter strings can contain private data; diagnostics do not export them.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The current history exceeds the bounds or has no route metadata.</exception>
    public PlatformApplicationStateData CaptureState(string stateKey)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        string prefix = StatePrefix(stateKey);
        var stack = navigator.NavigationStack;
        if (stack.Count > MaximumSavedRouteCount) throw new InvalidOperationException("The shell history is too deep for a bounded state handoff.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        { [prefix + "count"] = stack.Count.ToString(CultureInfo.InvariantCulture) };
        int total = prefix.Length + "count".Length + values[prefix + "count"].Length;
        for (int index = 0; index < stack.Count; index++)
        {
            if (!visits.TryGetValue(stack[index], out var context)) throw new InvalidOperationException("A retained page has no canonical route metadata.");
            string key = prefix + index.ToString(CultureInfo.InvariantCulture);
            if (context.Route.Length > PlatformApplicationStateData.MaximumValueLength || key.Length + context.Route.Length > PlatformApplicationStateData.MaximumTotalLength - total)
                throw new InvalidOperationException("The shell route history exceeds the bounded application state budget.");
            values.Add(key, context.Route);
            total += key.Length + context.Route.Length;
        }
        return new PlatformApplicationStateData(SavedStateVersion, values);
    }

    /// <summary>Attempts to rebuild a fresh, empty shell from a route-only application state handoff.</summary>
    /// <param name="data">Borrowed immutable data; unsupported schema, namespace or route data produces false.</param>
    /// <param name="stateKey">The same explicit identifier used by CaptureState.</param>
    /// <param name="animated">Whether to animate only the final restored visit; default false.</param>
    /// <param name="cancellationToken">Stops further work; an already committed prefix remains retained.</param>
    /// <returns>True when all visits remain current; false for an existing user history, invalid data, veto or supersession.</returns>
    /// <remarks>
    /// UI thread only. All bounded strings and route registrations are checked before any factory is
    /// invoked. Factories and admission then follow ordinary GoToAsync semantics. Each visit commits
    /// through NavigationPage; callbacks can interrupt a committed prefix, which is never rewound over
    /// newer work. A retained populated shell ignores an old recreation snapshot. This API performs
    /// no disk I/O, protocol registration, arbitrary deserialization or implicit active-history reset.
    /// Caller cancellation cancels the restore task; it does not dispose transferred/unadmitted pages.
    /// </remarks>
    public Task<bool> RestoreStateAsync(PlatformApplicationStateData data, string stateKey, bool animated = false, CancellationToken cancellationToken = default)
        => RestoreStateCore(data, stateKey, animated, cancellationToken, null);

    internal Task<bool> RestoreStateGuarded(PlatformApplicationStateData data, string stateKey, CancellationToken token, Func<bool> canContinue)
        => RestoreStateCore(data, stateKey, false, token, canContinue);

    private Task<bool> RestoreStateCore(PlatformApplicationStateData data, string stateKey, bool animated, CancellationToken cancellationToken, Func<bool>? canContinue)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        ArgumentNullException.ThrowIfNull(data);
        string prefix = StatePrefix(stateKey);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<bool>(cancellationToken);
        if (!(canContinue?.Invoke() ?? true)) return Task.FromResult(false);
        if (navigator.NavigationStack.Count != 0 || !TryReadRoutes(data, prefix, out string[] routes)) return Task.FromResult(false);
        if (requestDepth >= 32) throw new InvalidOperationException("Shell restore callbacks did not settle after 32 nested calls.");
        requestDepth++;
        try
        {
            userIntentVersion++;
            long intent = RetireRequest();
            if (retiring || version != intent || navigator.NavigationStack.Count != 0) return Task.FromResult(false);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = RestoreAsync(routes, intent, mountVersion, animated, cancellationToken, completion, canContinue);
            return completion.Task;
        }
        finally { requestDepth--; }
    }

    private async AnimationCompletion<bool> RestoreAsync(string[] routes, long expectedVersion, long expectedMount, bool animated, CancellationToken token, TaskCompletionSource<bool> completion, Func<bool>? canContinue)
    {
        var scheduler = AnimationScheduler.Default;
        try
        {
            for (int index = 0; index < routes.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                if (!RestoreCurrent(expectedVersion, expectedMount, canContinue) || navigator.NavigationStack.Count != index)
                { completion.TrySetResult(false); return false; }
                Task<bool> visit = GoToCore(routes[index], animated && index == routes.Length - 1, token, canContinue);
                expectedVersion = version;
                bool accepted = await Observe(visit).On(scheduler);
                if (!HasAccess(scheduler)) throw new InvalidOperationException("The shell restore dispatcher is unavailable.");
                token.ThrowIfCancellationRequested();
                if (!accepted || !RestoreCurrent(expectedVersion, expectedMount, canContinue) || navigator.NavigationStack.Count != index + 1 || CurrentRoute?.Route != routes[index])
                { completion.TrySetResult(false); return false; }
            }
            UpdateCommands();
            bool applied = RestoreCurrent(expectedVersion, expectedMount, canContinue);
            completion.TrySetResult(applied);
            return applied;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { completion.TrySetCanceled(token); return false; }
        catch (Exception error) { completion.TrySetException(error); return false; }
    }

    private bool RestoreCurrent(long expectedVersion, long expectedMount, Func<bool>? canContinue)
        => !retiring && version == expectedVersion && mountVersion == expectedMount && (canContinue?.Invoke() ?? true) &&
            !retiring && version == expectedVersion && mountVersion == expectedMount;

    private bool TryReadRoutes(PlatformApplicationStateData data, string prefix, out string[] routes)
    {
        routes = [];
        if (data.Version != SavedStateVersion || !data.Values.TryGetValue(prefix + "count", out string? rawCount) ||
            !int.TryParse(rawCount, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0 || count > MaximumSavedRouteCount ||
            rawCount != count.ToString(CultureInfo.InvariantCulture) || data.Values.Keys.Count(key => key.StartsWith(prefix, StringComparison.Ordinal)) != count + 1) return false;
        var copy = new string[count];
        for (int index = 0; index < count; index++)
        {
            if (!data.Values.TryGetValue(prefix + index.ToString(CultureInfo.InvariantCulture), out string? route) || !route.StartsWith('/')) return false;
            try
            {
                var context = Routes.Match(route);
                if (context.Route != route || index > 0 && copy[index - 1] == route) return false;
            }
            catch (ArgumentException) { return false; }
            catch (KeyNotFoundException) { return false; }
            copy[index] = route;
        }
        routes = copy;
        return true;
    }

    private static string StatePrefix(string stateKey)
    {
        ArgumentNullException.ThrowIfNull(stateKey);
        if (stateKey.Length == 0 || stateKey.Length > 64 || !(char.IsAsciiLetter(stateKey[0]) || stateKey[0] == '_') ||
            !stateKey.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
            throw new ArgumentException("A shell state key must be a bounded ASCII identifier.", nameof(stateKey));
        return "mfn.shell." + stateKey + ".";
    }
}
