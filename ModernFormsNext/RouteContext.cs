using System.Collections.ObjectModel;

namespace ModernFormsNext;

/// <summary>Contains the immutable route data supplied to an explicitly invoked page factory.</summary>
/// <remarks>
/// Route and parameter strings are application data, never code, file access or permission grants.
/// Values are decoded once using strict UTF-8. Keys use ordinal comparison. This object contains
/// no page, native handle, service locator or factory and can be retained independently of a visit.
/// </remarks>
public sealed class RouteContext
{
    internal RouteContext(string route, string pattern, IDictionary<string, string> parameters)
    {
        Route = route;
        Pattern = pattern;
        Parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters, StringComparer.Ordinal));
    }

    /// <summary>Gets the absolute logical path and canonical, ordinally sorted query string.</summary>
    /// <remarks>This is a logical route beginning with one slash, not an external absolute URI.</remarks>
    public string Route { get; }

    /// <summary>Gets the registered normalized pattern which matched this visit.</summary>
    public string Pattern { get; }

    /// <summary>Gets copied path and query parameters, with no duplicate or conflicting keys.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
