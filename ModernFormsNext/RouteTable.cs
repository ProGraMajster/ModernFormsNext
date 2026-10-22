using System.Text;

namespace ModernFormsNext;

/// <summary>Registers instance-owned page factories and matches bounded logical routes without rendering.</summary>
/// <remarks>
/// Use on the creating thread. Patterns contain literals and whole-segment {name} parameters only;
/// there are no wildcards, optional segments, regular expressions or reflection-based factories.
/// Matching is ordinal and case-sensitive. At the first differing segment a literal wins over a
/// parameter. Overlapping patterns with equal precedence are rejected, independent of registration order.
/// Factories are borrowed delegates: unregistering never disposes them or pages created earlier.
/// </remarks>
/// <example><code>
/// var routes = new RouteTable();
/// routes.Register("documents/{id}", route =&gt;
///     new ContentPage { Title = route.Parameters["id"] });
/// RouteContext match = routes.Match("/documents/42?mode=edit"); // Does not invoke the factory.
/// </code></example>
public sealed class RouteTable
{
    /// <summary>Gets the maximum number of registrations in one table.</summary>
    public const int MaximumRouteCount = 256;
    /// <summary>Gets the maximum UTF-16 length of a requested, normalized or registered route.</summary>
    public const int MaximumRouteLength = 4096;
    /// <summary>Gets the maximum number of logical path segments.</summary>
    public const int MaximumSegmentCount = 32;
    /// <summary>Gets the maximum combined number of path and query parameters.</summary>
    public const int MaximumParameterCount = 32;
    /// <summary>Gets the maximum UTF-16 length of a parameter name.</summary>
    public const int MaximumParameterNameLength = 128;
    /// <summary>Gets the maximum UTF-16 length of a decoded parameter value.</summary>
    public const int MaximumParameterValueLength = 4096;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly List<Registration> registrations = [];
    private bool retired;
    internal event Action? Changed;

    /// <summary>Creates an empty table without invoking factories or initializing a UI runtime.</summary>
    public RouteTable() { }

    /// <summary>Gets the number of active registrations on the creating thread.</summary>
    public int Count { get { VerifyAccess(); return registrations.Count; } }

    /// <summary>Registers a synchronous application factory without invoking it.</summary>
    /// <param name="pattern">An optional leading slash, literals and whole-segment named parameters.</param>
    /// <param name="factory">A borrowed delegate returning a detached page when navigation explicitly requests it.</param>
    /// <returns>A lease whose disposal unregisters exactly this registration on the creating thread.</returns>
    /// <remarks>Disposing an already retired lease is harmless. A table does not cache factory results.</remarks>
    public IDisposable Register(string pattern, Func<RouteContext, Page> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return Register(pattern, (context, _) => Task.FromResult(factory(context)));
    }

    /// <summary>Registers a borrowed asynchronous application factory without invoking it.</summary>
    /// <param name="pattern">The bounded logical pattern; equivalent parameter shapes are ambiguous.</param>
    /// <param name="factory">A UI-thread-invoked delegate. Its returned page stays caller-owned until host admission.</param>
    /// <returns>A creating-thread lease that removes this exact registration without disposing pages.</returns>
    /// <remarks>
    /// The caller controls asynchronous work. Unregistering does not forcibly stop application code.
    /// Registration commits before an owning shell notifies command observers. If an observer throws,
    /// Unregister(pattern) can remove that committed registration even though no lease was returned.
    /// </remarks>
    public IDisposable Register(string pattern, Func<RouteContext, CancellationToken, Task<Page>> factory)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(factory);
        if (registrations.Count == MaximumRouteCount) throw new InvalidOperationException("The route table is full.");
        Segment[] segments = ParsePattern(pattern, out string normalized);
        if (registrations.Any(existing => SameShape(existing.Segments, segments)))
            throw new ArgumentException("An equivalent route pattern is already registered.", nameof(pattern));
        var registration = new Registration(this, normalized, segments, factory);
        registrations.Add(registration);
        Changed?.Invoke();
        return registration;
    }

    /// <summary>Removes the exact normalized pattern without disposing its borrowed factory or existing pages.</summary>
    /// <param name="pattern">The original pattern, including its parameter names; leading/trailing slashes normalize away.</param>
    /// <returns>True if this exact pattern was registered; false otherwise.</returns>
    /// <remarks>
    /// Creating thread only. Removal commits before shell command observers run; their exceptions
    /// propagate without restoring the registration. A later disposal of its old lease is harmless.
    /// </remarks>
    public bool Unregister(string pattern)
    {
        VerifyAccess();
        ParsePattern(pattern, out string normalized);
        var registration = registrations.Find(item => StringComparer.Ordinal.Equals(item.Pattern, normalized));
        if (registration is null) return false;
        Unregister(registration);
        return true;
    }

    /// <summary>Unregisters all factories without disposing existing pages or borrowed delegates.</summary>
    /// <remarks>The change commits before notification to an owning shell. Use on the creating thread.</remarks>
    public void Clear()
    {
        VerifyAccess();
        if (registrations.Count == 0) return;
        foreach (var registration in registrations) registration.Deactivate();
        registrations.Clear();
        Changed?.Invoke();
    }

    /// <summary>Matches a logical route and returns copied data without invoking its page factory.</summary>
    /// <param name="route">An absolute logical path, or a path appended to the current logical path.</param>
    /// <param name="relativeTo">An absolute logical base, or null for the route root. Its query is not inherited.</param>
    /// <returns>Immutable normalized data for the unique highest-precedence registration.</returns>
    /// <exception cref="ArgumentException">Syntax, encoding, parameter conflicts or documented bounds are invalid.</exception>
    /// <exception cref="KeyNotFoundException">No active registration matches the normalized path.</exception>
    /// <remarks>
    /// Empty path segments and a trailing slash normalize away. Decoded dot segments are resolved;
    /// traversing above root, encoded path separators, fragments and authority-style // input are rejected.
    /// Query plus signs are literal plus signs; use percent-encoded spaces. Repeated query keys and
    /// conflicts with path parameter names are rejected. Each string is decoded exactly once.
    /// </remarks>
    public RouteContext Match(string route, string? relativeTo = null) => Resolve(route, relativeTo).Context;

    // Lifecycle URI mapping shares the exact logical codec without requiring a registration
    // or invoking a factory merely to compare an explicitly allowed URI base.
    internal static string Normalize(string route) => ParseRoute(route, null).Route;

    internal ResolvedRoute Resolve(string route, string? relativeTo = null)
    {
        VerifyAccess();
        var parsed = ParseRoute(route, relativeTo);
        Registration? best = null;
        foreach (var registration in registrations)
        {
            if (registration.Segments.Length != parsed.Segments.Count) continue;
            bool matches = true;
            for (int index = 0; index < registration.Segments.Length; index++)
                if (!registration.Segments[index].Parameter && !StringComparer.Ordinal.Equals(registration.Segments[index].Value, parsed.Segments[index]))
                { matches = false; break; }
            if (matches && (best is null || ComparePrecedence(registration.Segments, best.Segments) > 0)) best = registration;
        }
        if (best is null) throw new KeyNotFoundException("No registered route matches the requested logical path.");
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < best.Segments.Length; index++)
            if (best.Segments[index].Parameter) AddParameter(parameters, best.Segments[index].Value, parsed.Segments[index]);
        foreach (var parameter in parsed.Query) AddParameter(parameters, parameter.Key, parameter.Value);
        return new(best, new RouteContext(parsed.Route, best.Pattern, parameters));
    }

    internal void Retire()
    {
        if (retired) return;
        Clear();
        retired = true;
        Changed = null;
    }

    private void Unregister(Registration registration)
    {
        if (!registration.Active) return;
        VerifyAccess();
        registration.Deactivate();
        registrations.Remove(registration);
        Changed?.Invoke();
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(retired, this);
        if (threadId != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Route tables require their creating thread.");
    }

    private static Segment[] ParsePattern(string pattern, out string normalized)
    {
        ValidateText(pattern, nameof(pattern));
        if (pattern.Contains('?')) throw new ArgumentException("Route patterns cannot contain a query.", nameof(pattern));
        var segments = new List<Segment>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            bool parameter = raw.StartsWith('{') && raw.EndsWith('}');
            string value;
            if (parameter)
            {
                value = raw[1..^1];
                if (value.Length == 0 || value.Length > MaximumParameterNameLength || !IsIdentifier(value) || !names.Add(value))
                    throw new ArgumentException("Route parameter names must be unique bounded identifiers.", nameof(pattern));
            }
            else
            {
                if (raw.Contains('{') || raw.Contains('}')) throw new ArgumentException("A route parameter must occupy one complete segment.", nameof(pattern));
                value = Decode(raw, pathSegment: true);
                if (value is "." or "..") throw new ArgumentException("A route pattern cannot contain dot segments.", nameof(pattern));
            }
            segments.Add(new(value, parameter));
            if (segments.Count > MaximumSegmentCount) throw new ArgumentException("The route has too many segments.", nameof(pattern));
        }
        normalized = "/" + string.Join('/', segments.Select(segment => segment.Parameter ? "{" + segment.Value + "}" : Uri.EscapeDataString(segment.Value)));
        if (normalized.Length > MaximumRouteLength) throw new ArgumentException("The normalized pattern is too long.", nameof(pattern));
        return segments.ToArray();
    }

    private static ParsedRoute ParseRoute(string route, string? relativeTo)
    {
        ValidateText(route, nameof(route));
        int queryIndex = route.IndexOf('?');
        string path = queryIndex < 0 ? route : route[..queryIndex];
        var segments = new List<string>();
        if (!path.StartsWith('/') && relativeTo is not null)
        {
            if (!relativeTo.StartsWith('/')) throw new ArgumentException("A relative route base must be an absolute logical path.", nameof(relativeTo));
            segments.AddRange(ParseRoute(relativeTo, null).Segments);
        }
        foreach (string raw in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string value = Decode(raw, pathSegment: true);
            if (value == ".") continue;
            if (value == "..")
            {
                if (segments.Count == 0) throw new ArgumentException("A route cannot traverse above its root.", nameof(route));
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(value);
            if (segments.Count > MaximumSegmentCount) throw new ArgumentException("The route has too many segments.", nameof(route));
        }
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (queryIndex >= 0 && queryIndex < route.Length - 1)
            foreach (string pair in route[(queryIndex + 1)..].Split('&'))
            {
                int equals = pair.IndexOf('=');
                string key = Decode(equals < 0 ? pair : pair[..equals], pathSegment: false);
                string value = equals < 0 ? string.Empty : Decode(pair[(equals + 1)..], pathSegment: false);
                if (query.Count == MaximumParameterCount || string.IsNullOrWhiteSpace(key) || key.Length > MaximumParameterNameLength || value.Length > MaximumParameterValueLength || !query.TryAdd(key, value))
                    throw new ArgumentException("Query parameters must be bounded and have unique nonblank ordinal keys.", nameof(route));
            }
        string normalized = "/" + string.Join('/', segments.Select(Uri.EscapeDataString));
        if (query.Count > 0) normalized += "?" + string.Join('&', query.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        if (normalized.Length > MaximumRouteLength) throw new ArgumentException("The normalized route is too long.", nameof(route));
        return new(normalized, segments, query);
    }

    private static void ValidateText(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length == 0 || value.Length > MaximumRouteLength || value.StartsWith("//", StringComparison.Ordinal) || value.Contains('#') || value.Any(char.IsControl))
            throw new ArgumentException("The logical route has invalid syntax or exceeds its documented bound.", name);
    }

    private static string Decode(string value, bool pathSegment)
    {
        // Decode percent bytes and literal UTF-16 through the same strict UTF-8 stream.
        // Uri.UnescapeDataString alone permits malformed escapes/UTF-8 to survive silently.
        var bytes = new List<byte>(value.Length);
        try
        {
            for (int index = 0; index < value.Length;)
            {
                if (value[index] == '%')
                {
                    if (index + 2 >= value.Length || !byte.TryParse(value.AsSpan(index + 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out byte encoded))
                        throw new ArgumentException("The route contains a malformed percent escape.");
                    bytes.Add(encoded); index += 3;
                }
                else
                {
                    int end = value.IndexOf('%', index);
                    if (end < 0) end = value.Length;
                    bytes.AddRange(StrictUtf8.GetBytes(value[index..end]));
                    index = end;
                }
            }
            string decoded = StrictUtf8.GetString(bytes.ToArray());
            if (decoded.Any(char.IsControl) || pathSegment && (decoded.Contains('/') || decoded.Contains('\\')))
                throw new ArgumentException("A decoded route segment contains a forbidden control or separator.");
            return decoded;
        }
        catch (DecoderFallbackException error) { throw new ArgumentException("Route percent bytes must contain valid UTF-8.", error); }
        catch (EncoderFallbackException error) { throw new ArgumentException("Route text must contain valid Unicode.", error); }
    }

    private static void AddParameter(Dictionary<string, string> parameters, string key, string value)
    {
        if (parameters.Count == MaximumParameterCount || key.Length > MaximumParameterNameLength || value.Length > MaximumParameterValueLength || !parameters.TryAdd(key, value))
            throw new ArgumentException("Path and query parameters must be bounded and have distinct ordinal keys.");
    }
    private static bool IsIdentifier(string value)
        => (char.IsAsciiLetter(value[0]) || value[0] == '_') && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    private static bool SameShape(Segment[] left, Segment[] right)
        => left.Length == right.Length && left.Zip(right).All(pair => pair.First.Parameter == pair.Second.Parameter && (pair.First.Parameter || StringComparer.Ordinal.Equals(pair.First.Value, pair.Second.Value)));
    private static int ComparePrecedence(Segment[] left, Segment[] right)
    {
        for (int index = 0; index < left.Length; index++)
            if (left[index].Parameter != right[index].Parameter) return left[index].Parameter ? -1 : 1;
        return 0;
    }

    internal readonly record struct ResolvedRoute(Registration Registration, RouteContext Context);
    internal readonly record struct Segment(string Value, bool Parameter);
    private sealed record ParsedRoute(string Route, List<string> Segments, SortedDictionary<string, string> Query);
    internal sealed class Registration(RouteTable owner, string pattern, Segment[] segments, Func<RouteContext, CancellationToken, Task<Page>> factory) : IDisposable
    {
        private RouteTable? owner = owner;
        internal string Pattern { get; } = pattern;
        internal Segment[] Segments { get; } = segments;
        internal Func<RouteContext, CancellationToken, Task<Page>>? Factory { get; private set; } = factory;
        internal bool Active => owner is not null;
        internal void Deactivate() { owner = null; Factory = null; }
        public void Dispose() => owner?.Unregister(this);
    }
}
