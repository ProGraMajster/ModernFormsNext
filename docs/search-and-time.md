# SearchBar and TimePicker

These controls are in the 1.12.0 development source; this does not announce a
published package. Windows is the primary runtime. Android remains Experimental.
All control operations and result publication require the owning UI thread.

## Search and suggestions

`SearchBar` owns an actual `TextBox`, Search and Clear buttons, and one
`CollectionView` inside the existing popup. It shares text composition, command
routing, binding, focus and accessibility with those controls. Suggestions keep
the editor's native IME connection. There is no additional text engine or item cache.

```csharp
var search = new SearchBar { Placeholder = "Find a document", HistoryCapacity = 32 };
search.SuggestionProvider = query => documents
    .Where(name => name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
    .Take(100).ToArray();
search.SearchCommand = new DelegateCommand(value => OpenResults((string)value!));
search.SearchRequested += (_, e) => e.Cancel = !MaySearch(e.Query);
```

Providers run on the UI thread, once per coalesced request after text/composition
callbacks. Keep them short. `SuggestionsSource` accepts a borrowed indexed `IList`;
observable sources use the normal collection mutation path. `SuggestionItemTemplate`
borrows the existing `IItemContainerFactory<Control>` contract; see
[CollectionView](collection-view.md) for root ownership and binding retirement.
`SuggestionTextSelector` converts the chosen value to editor text independently
of its template. Results, factories, commands and source values remain application-owned.

The popup shows at most `MaximumVisibleSuggestions` rows (default 6, range 1–20).
`SuggestionRowHeight` is a positive logical-pixel height, default 32. Its canonical
virtualizer realizes the viewport plus two cached items; a large indexed source
does not create one control per result. The highlighted occurrence is
`HighlightedSuggestionIndex`, with -1 meaning none. Changing text retires previous
results. An empty result and a pending request have distinct status captions.

Down/Up select results while the editor retains focus. Enter chooses a highlighted
result or submits the query; Escape dismisses; Tab dismisses and follows normal
focus navigation. Pointer and accessible Invoke use the same collection route.
Leaving the editor dismisses suggestions and retires pending results; a focus
redirection from a suggestion callback prevents its old commit.
Selection commits on the next dispatcher turn, after the original input/Invoke
route returns. `SuggestionChosen` can cancel before text changes.
`SubmitOnSuggestion` defaults to true. Composition is allowed to finish before
providers, suggestions or submission are admitted. The software keyboard Search
action uses the existing editor-action route.

Submission checks availability, raises the actual Search button's Click, then
`SearchRequested`, publishes history and finally executes the canonical command.
Recursive submission is ignored. A callback that changes text/bindings, hides,
reparents or disposes the host prevents obsolete execution. The default command
parameter is the query; a nonnull `SearchCommandParameter` overrides it.
`SearchCommandTarget` supports ordinary routed commands. `AsyncCommand` owns busy,
completion, fault and cancellation state. SearchBar never cancels or disposes a
borrowed command; its search button reflects normal command availability.

## Application-owned asynchronous results

The control creates no background job, timer, networking or persistence.
`SuggestionsRequested` supplies an immutable query snapshot and a revocable weak
request. The application owns cancellation and operation lifetime:

```csharp
search.SuggestionsRequested += async (_, request) =>
{
    try
    {
        var results = await repository.FindAsync(request.Query, applicationToken);
        Application.RunOnUIThread(() => request.TrySetSuggestions(results));
    }
    catch (OperationCanceledException) { /* Application-owned cancellation. */ }
    catch (Exception error)
    {
        Log(error);
        Application.RunOnUIThread(() => request.TrySetError("Search is unavailable"));
    }
};
```

`Query` may be read by background work. `IsCurrent`, `TrySetSuggestions` and
`TrySetError` require the UI thread. A newer query/provider, dismissal, reparenting,
disposal or newer popup rejects stale results. A synchronous event publication
takes precedence over the local provider. Without a provider or event handler,
bounded recent history supplies prefix matches. A synchronous provider/event
failure ends loading and uses a generic caption while the original exception
continues through normal dispatcher diagnostics. Error captions are application
text; avoid sensitive query or exception data.

History contains at most `HistoryCapacity` strings (default 32, range 0–1000),
most recent first, deduplicated with ordinal case-insensitive comparison.
Whitespace-only queries are not submitted. `History` is a detached snapshot.
`ClearHistory`, capacity reduction, or disabling `IsHistoryEnabled` removes owned
entries. No history is written to disk. Applications choose any persistence policy.
Explicit history or retention changes refresh suggestions after `HistoryChanged` so
an old history result array is released; newer callback publications take precedence.

## Nullable time without a date or time zone

```csharp
var time = new TimePicker
{
    Time = new TimeOnly(14, 30, 15).Add(TimeSpan.FromTicks(1234567)),
    MinimumTime = new TimeOnly(9, 0),
    MaximumTime = new TimeOnly(18, 0),
    CultureName = "pl-PL",
    Format = "HH:mm:ss.fffffff",
    StepSeconds = 30
};
time.TimeChanged += (_, _) => SaveOptionalTime(time.Time);
time.Time = null;
```

TimePicker owns one canonical `DateTimePicker` spinner. `Time` is `TimeOnly?`;
the checkbox activates/clears it. All 100-nanosecond ticks are retained, even when
the format hides precision. The internal unspecified date transports ticks only:
there is no date, time zone or daylight-saving conversion in the public contract.
Bounds are inclusive, default to the whole day, and reject wrapping ranges.
Changing bounds clamps a selected value while an empty value stays empty.
An explicit nonnull assignment activates the retained value, even if equal.

`StepSeconds` ranges from 1 through 86400, default 60. Arrows, wheel and actual
spinner buttons step within bounds without wrapping midnight. This is the
existing spinner presentation, not a segmented keyboard text field or native
platform time dialog. `Text` assignment and the spinner's accessible SetValue
action parse a time; whitespace clears it. An explicit `Format` uses exact
parsing. Null format uses the culture's short-time display and time parser.
`CultureName == null` uses current UI-thread culture; empty uses invariant culture.
Examples include `HH:mm`, `hh:mm tt`, `HH:mm:ss` and fractional seconds.

`TimeChanged` observes committed effective nullable values. Formatting raises
`TextChanged` without changing stored ticks. Reentrant value/range/format changes
keep the newest state. `FocusEditor()` uses canonical focus admission for both
controls. Actual implicit editor/button peers provide accessibility; popup
occurrences appear only under their real popup root.
Native host deactivation or recreation can dismiss suggestions and retire an IME
session. A later activation acquires a new session through normal focus admission;
stored query text and time values are independent of that native session identity.

## Designer and examples

The known-framework Designer creates inert, bounded previews. Authored scalar
properties round-trip through `.mfdesign`, generated C#, parser and runtime;
providers, sources, templates, commands, history and loading state are runtime-only.
TimeOnly uses a bounded exact-tick data value and a fully-qualified literal
`new global::System.TimeOnly(ticksL)`. Parsing never evaluates project code,
constructors, converters, `Parse`, `Now` or arbitrary expressions. Invalid ranges,
formats and limits are rejected by normal transactional Property Grid editing.
Implicit editors/buttons are not authored children in generated forms.

ControlGallery's **Search / Time** page demonstrates local and explicitly deferred
results, stale publication rejection, errors, recent history, nullable time and
12/24-hour presentation. It performs no networking or disk persistence. Human IME,
screen-reader and physical-keyboard observations are separate from automated
host/native input evidence; none is implied by a headless or preview test.
