using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace ModernFormsNext.Designer.Surface;

// Detached framework controls have no native window or host services. Their Site is
// available to property setters, layout and paint after the trusted constructor returns.
internal static class DesignerPreviewControl
{
    private const string StackSeparator = " | stack: ";
    internal static void Prepare(Control control)
    {
        control.Site = new PreviewSite(control);
        if (control is SearchBar search)
            search.SuppressSuggestionRequests = true;
        if (control is DocumentViewer viewer)
            viewer.SuppressDesignTimeImageLoading();
        else if (control is MarkdownEditor markdown)
            markdown.PreviewViewer.SuppressDesignTimeImageLoading();
    }

    internal static string Failure(string stage, Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } cause })
            exception = cause;

        string message = exception.Message.ReplaceLineEndings(" ");
        if (message.Length > 512)
            message = message[..512] + "…";

        // Keep the originating methods without source file paths and bound the log entry.
        var methods = new List<string>(5);
        var stack = new StackTrace(exception, fNeedFileInfo: false);
        for (int index = 0; index < Math.Min(stack.FrameCount, 5); index++)
        {
            var method = stack.GetFrame(index)?.GetMethod();
            // Reflection switches from interpreted calls to generated invocation stubs after
            // warmup. Stop before those frames so the same fault has a stable log signature.
            if (method?.DeclaringType is not { } owner || owner.Namespace == "System.Reflection")
                break;
            methods.Add($"{owner.FullName}.{method.Name}");
        }
        string failure = $"{stage}: {exception.GetType().Name}: {message}";
        return methods.Count == 0 ? failure : $"{failure}{StackSeparator}{string.Join(" ← ", methods)}";
    }

    // JIT and reflection can change the frames for the exact same stage and exception.
    // Keep those details in Output, but compare the deterministic part of the diagnostic.
    internal static string FailureSignature(string diagnostic)
    {
        int separator = diagnostic.IndexOf(StackSeparator, StringComparison.Ordinal);
        return separator < 0 ? diagnostic : diagnostic[..separator];
    }

    internal static string? Dispose(Control control)
    {
        try
        {
            control.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return Failure($"Dispose {control.Name} ({control.GetType().Name})", exception);
        }
    }

    private sealed class PreviewSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }
}
