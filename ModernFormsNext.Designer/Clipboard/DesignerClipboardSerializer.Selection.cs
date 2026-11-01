using System.Text.Json;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Clipboard;

internal static partial class DesignerClipboardSerializer
{
    internal static bool TrySerializeSelection(IReadOnlyList<DesignControlNode> nodes,
        out string? content, out string? error, IReadOnlyList<DesignBounds>? rootBounds = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        content = null;
        try
        {
            ValidateSelectionData(nodes);
            if (rootBounds is not null && rootBounds.Count != nodes.Count)
                throw new ArgumentException("Clipboard root bounds must match the selection.", nameof(rootBounds));
            int remaining = MaximumNodeCount;
            var roots = new List<DesignerClipboardNode>(nodes.Count);
            for (int index = 0; index < nodes.Count; index++)
            {
                var root = ToClipboardNode(nodes[index], 0, ref remaining);
                if (rootBounds is not null)
                {
                    var bounds = rootBounds[index];
                    if (bounds.Width < 0 || bounds.Height < 0 ||
                        (long)bounds.X + bounds.Width > int.MaxValue || (long)bounds.Y + bounds.Height > int.MaxValue)
                        throw new ArgumentException("Clipboard bounds exceed the supported coordinate range.", nameof(rootBounds));
                    root.X = bounds.X; root.Y = bounds.Y;
                    root.Width = bounds.Width; root.Height = bounds.Height;
                }
                roots.Add(root);
            }
            var payload = new DesignerClipboardPayload
            {
                Format = DesignerClipboardPayload.CurrentFormat,
                Version = roots.Count == 1 ? DesignerClipboardPayload.CurrentVersion : DesignerClipboardPayload.ForestVersion,
                Root = roots.Count == 1 ? roots[0] : null,
                Roots = roots.Count == 1 ? null : roots
            };
            var encoded = JsonSerializer.Serialize(payload, Options);
            if (encoded.Length > MaximumContentLength)
                throw new InvalidOperationException("The selected controls exceed the Designer clipboard text budget.");
            content = encoded;
            error = null;
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or NotSupportedException or JsonException)
        {
            error = $"Cannot copy the selected controls: {failure.Message}";
            return false;
        }
    }

    internal static bool TryDeserializeSelection(string? content,
        out IReadOnlyList<DesignControlNode>? nodes, out string? error)
    {
        nodes = null;
        if (string.IsNullOrWhiteSpace(content)) { error = "The Designer clipboard is empty."; return false; }
        if (content.Length > MaximumContentLength) { error = "The Designer clipboard payload exceeds the supported size limit."; return false; }
        try
        {
            var payload = JsonSerializer.Deserialize<DesignerClipboardPayload>(content, Options)
                ?? throw new JsonException("The Designer clipboard did not contain a payload.");
            if (!string.Equals(payload.Format, DesignerClipboardPayload.CurrentFormat, StringComparison.Ordinal))
                throw new JsonException("Unsupported Designer clipboard format.");
            IReadOnlyList<DesignerClipboardNode> roots;
            if (payload.Version == DesignerClipboardPayload.CurrentVersion && payload.Root is not null && payload.Roots is null)
                roots = [payload.Root];
            else if (payload.Version == DesignerClipboardPayload.ForestVersion && payload.Root is null && payload.Roots is { Count: > 0 } forest)
                roots = forest;
            else
                throw new JsonException("Unsupported or ambiguous Designer clipboard version and roots.");
            if (roots.Count > MaximumNodeCount)
                throw new JsonException("The Designer clipboard exceeds the control budget.");
            int remaining = MaximumNodeCount;
            var decoded = new List<DesignControlNode>(roots.Count);
            foreach (var root in roots)
            {
                if (root is null) throw new JsonException("A clipboard control root must not be null.");
                decoded.Add(FromClipboardNode(root, 0, ref remaining));
            }
            ValidateSelectionData(decoded);
            nodes = decoded.AsReadOnly();
            error = null;
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or NotSupportedException or JsonException)
        {
            error = $"Cannot paste the Designer clipboard payload: {failure.Message}";
            return false;
        }
    }

    private static void ValidateSelectionData(IReadOnlyList<DesignControlNode> nodes)
    {
        if (nodes.Count == 0 || nodes.Count > MaximumNodeCount)
            throw new InvalidOperationException("A clipboard selection must fit the Designer control budget.");
        // Borrow nodes only for canonical data admission. The shared validator checks cycles,
        // total controls, depth, all property/event values and combined text before encoding;
        // separate roots do not each receive a fresh budget. No runtime types are constructed.
        var data = new DesignDocument();
        foreach (var node in nodes) data.Controls.Add(node);
        var validation = new DesignDocumentValidator().ValidateStructure(data);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
    }
}
