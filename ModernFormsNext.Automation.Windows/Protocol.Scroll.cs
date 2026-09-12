using System.Drawing;
using System.Text.Json;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation.Windows;

internal sealed record ScrollOperation(int Kind, double? Horizontal, double? Vertical, int HorizontalAmount, int VerticalAmount)
{
    internal static ScrollOperation? From(AccessibleScrollRequest? request) => request is null ? null
        : new((int)request.Kind, request.Horizontal, request.Vertical, (int)request.HorizontalAmount, (int)request.VerticalAmount);
    internal AccessibleScrollRequest ToRequest()
    {
        try
        {
            return Kind switch
            {
                0 when Horizontal is null && Vertical is null => AccessibleScrollRequest.ByAmount((AccessibleScrollAmount)HorizontalAmount, (AccessibleScrollAmount)VerticalAmount),
                1 when HorizontalAmount == 0 && VerticalAmount == 0 => AccessibleScrollRequest.ToPercent(Horizontal, Vertical),
                2 when HorizontalAmount == 0 && VerticalAmount == 0 && Horizontal.HasValue && Vertical.HasValue => AccessibleScrollRequest.ByViewport(Horizontal.Value, Vertical.Value),
                _ => throw new AutomationTransportException(AutomationTransportError.InvalidRequest)
            };
        }
        catch (ArgumentException) { throw new AutomationTransportException(AutomationTransportError.InvalidRequest); }
    }
}

internal static partial class Protocol
{
    // Optional on protocol v1: readers accept older snapshots without the additive field.
    private static AccessibleScrollInfo? ReadScroll(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("scrollInfo", out var info) || info.ValueKind == JsonValueKind.Null) return null;
        static AccessibleScrollAxis Axis(JsonElement a) => new(a.GetProperty("offset").GetDouble(),
            a.GetProperty("minimum").GetDouble(), a.GetProperty("maximum").GetDouble(),
            a.GetProperty("viewportLength").GetDouble(), a.GetProperty("smallChange").GetDouble(), a.GetProperty("largeChange").GetDouble());
        var bounds = info.GetProperty("viewportBounds");
        return new(Axis(info.GetProperty("horizontal")), Axis(info.GetProperty("vertical")),
            new Rectangle(bounds.GetProperty("x").GetInt32(), bounds.GetProperty("y").GetInt32(),
                bounds.GetProperty("width").GetInt32(), bounds.GetProperty("height").GetInt32()));
    }
}
