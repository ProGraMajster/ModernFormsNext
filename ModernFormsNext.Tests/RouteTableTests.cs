using Xunit;

namespace ModernFormsNext.Tests;

public sealed class RouteTableTests
{
    [Fact]
    public void MatchIsDataOnlyAndCopiesDecodedOrdinalParameters()
    {
        var routes = new RouteTable();
        int calls = 0;
        using var registration = routes.Register("documents/{id}", _ => { calls++; return new ContentPage(); });
        var match = routes.Match("/documents/%C5%BC%C3%B3%C5%82w?mode=edit&plus=a+b");
        Assert.Equal(0, calls);
        Assert.Equal("/documents/{id}", match.Pattern);
        Assert.Equal("żółw", match.Parameters["id"]);
        Assert.Equal("a+b", match.Parameters["plus"]);
        Assert.Equal("/documents/%C5%BC%C3%B3%C5%82w?mode=edit&plus=a%2Bb", match.Route);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)match.Parameters).Add("other", "value"));
        Assert.Throws<KeyNotFoundException>(() => routes.Match("/Documents/42"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralPrecedenceIsLexicographicAndIndependentOfRegistrationOrder(bool reverse)
    {
        var routes = new RouteTable();
        string[] patterns = ["/fixed/{value}", "/{key}/fixed"];
        foreach (var pattern in reverse ? patterns.Reverse() : patterns) routes.Register(pattern, NeverInvoke);
        var match = routes.Match("/fixed/fixed");
        Assert.Equal("/fixed/{value}", match.Pattern);
        Assert.Equal("fixed", match.Parameters["value"]);
        routes.Register("/fixed/fixed", NeverInvoke);
        Assert.Equal("/fixed/fixed", routes.Match("/fixed/fixed").Pattern);
    }

    [Fact]
    public void RelativeAbsoluteAndDecodedDotSegmentsHaveOneCanonicalResult()
    {
        var routes = new RouteTable();
        routes.Register("/documents/{id}", NeverInvoke);
        var relative = routes.Match("../settings/?b=2&a=1", "/documents/42?discard=yes");
        Assert.Equal("/documents/settings?a=1&b=2", relative.Route);
        Assert.False(relative.Parameters.ContainsKey("discard"));
        Assert.Equal(relative.Route, routes.Match("/documents//old/%2e%2e/settings?a=1&b=2", "/unrelated").Route);
        Assert.Equal("/documents/1", routes.Match("/documents/1", "/unrelated/path").Route);
        Assert.Equal("/documents/42?mode=read", routes.Match("?mode=read", "/documents/42?old=value").Route);
        Assert.Throws<ArgumentException>(() => routes.Match("42", "documents"));
    }

    [Theory]
    [InlineData("//authority/x")]
    [InlineData("/../x")]
    [InlineData("/%2F")]
    [InlineData("/%5c")]
    [InlineData("/%FF")]
    [InlineData("/%C0%AF")]
    [InlineData("/%")]
    [InlineData("/x#fragment")]
    [InlineData("/x?key=%")]
    [InlineData("/x?x=1&x=2")]
    [InlineData("/x?%20=blank")]
    [InlineData("/x?tab=%09")]
    [InlineData("/x?x=% A")]
    public void MalformedOrAmbiguousInputsAreRejectedBeforeFactoryInvocation(string input)
    {
        var routes = new RouteTable();
        routes.Register("/{id}", NeverInvoke);
        Assert.Throws<ArgumentException>(() => routes.Match(input));
    }

    [Fact]
    public void ParameterConflictsAreOrdinalAndEscapesAreDecodedExactlyOnce()
    {
        var routes = new RouteTable();
        routes.Register("/{id}", NeverInvoke);
        Assert.Throws<ArgumentException>(() => routes.Match("/x?id=other"));
        var match = routes.Match("/%252F?ID=other&url=https%3A%2F%2Fexample.test%2Fa");
        Assert.Equal("%2F", match.Parameters["id"]);
        Assert.Equal("other", match.Parameters["ID"]);
        Assert.Equal("https://example.test/a", match.Parameters["url"]);
        Assert.Equal("😀", routes.Match("/%F0%9F%98%80").Parameters["id"]);
        Assert.Throws<ArgumentException>(() => routes.Match("/" + '\ud800'));
    }

    [Theory]
    [InlineData("/a/{id}/{id}")]
    [InlineData("/a/{with-dash}")]
    [InlineData("/a/prefix{id}")]
    [InlineData("/a/{}")]
    [InlineData("/a/..")]
    [InlineData("/a?query=1")]
    [InlineData("/a/%2f")]
    public void InvalidPatternsAreNotRegistered(string pattern)
    {
        var routes = new RouteTable();
        Assert.Throws<ArgumentException>(() => routes.Register(pattern, NeverInvoke));
        Assert.Equal(0, routes.Count);
    }

    [Fact]
    public void EquivalentShapesAreRejectedAndOldLeasesCannotUnregisterNewFactories()
    {
        var routes = new RouteTable();
        var first = routes.Register("/a/{id}", NeverInvoke);
        Assert.Throws<ArgumentException>(() => routes.Register("a/{name}/", NeverInvoke));
        first.Dispose();
        var second = routes.Register("a/{name}", NeverInvoke);
        first.Dispose();
        Assert.Equal(1, routes.Count);
        Assert.Equal("/a/{name}", routes.Match("/a/value").Pattern);
        routes.Clear(); second.Dispose();
        Assert.Equal(0, routes.Count);
        Assert.Throws<KeyNotFoundException>(() => routes.Match("/a/value"));
    }

    [Fact]
    public void DocumentedBoundsApplyBeforeUnknownRouteHandling()
    {
        var routes = new RouteTable();
        for (int index = 0; index < RouteTable.MaximumRouteCount; index++) routes.Register("r" + index, NeverInvoke);
        Assert.Throws<InvalidOperationException>(() => routes.Register("overflow", NeverInvoke));
        Assert.Throws<ArgumentException>(() => routes.Match(new string('x', RouteTable.MaximumRouteLength + 1)));
        Assert.Throws<ArgumentException>(() => routes.Match(new string('ą', 2000)));
        Assert.Throws<ArgumentException>(() => routes.Match("/" + string.Join('/', Enumerable.Repeat("x", RouteTable.MaximumSegmentCount + 1))));
        routes.Clear();
        routes.Register("/root", NeverInvoke);
        string query = string.Join('&', Enumerable.Range(0, RouteTable.MaximumParameterCount).Select(index => "p" + index + "=v"));
        Assert.Equal(RouteTable.MaximumParameterCount, routes.Match("/root?" + query).Parameters.Count);
        Assert.Throws<ArgumentException>(() => routes.Match("/root?" + query + "&overflow=v"));
        Assert.Throws<ArgumentException>(() => routes.Match("/root?" + new string('k', RouteTable.MaximumParameterNameLength + 1) + "=v"));
    }

    [Fact]
    public void RouteTablesRejectAnotherThreadWithoutInitializingAUiRuntime()
    {
        var routes = new RouteTable();
        using var registration = routes.Register("/x", NeverInvoke);
        Exception? error = null;
        var worker = new Thread(() => { try { routes.Match("/x"); } catch (Exception failure) { error = failure; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("/x", routes.Match("/x").Route);
    }

    private static Page NeverInvoke(RouteContext context) => throw new InvalidOperationException("Matching must not invoke a page factory.");
}
