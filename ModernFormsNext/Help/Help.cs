using ModernFormsNext.Layout;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace ModernFormsNext.Help;

/// <summary>
/// Provides WinForms-like helpers for displaying application help.
/// </summary>
/// <remarks>
/// This shared implementation is intentionally platform-neutral. It resolves local paths and
/// absolute URLs, then asks the operating system to open them with the registered handler.
/// Native help engines, such as Windows HTML Help, should be implemented by a backend-specific
/// service rather than by placing P/Invoke code in the shared framework assembly.
/// </remarks>
public static class Help
{
    /// <summary>
    /// Displays the contents of the help file or URL.
    /// </summary>
    /// <param name="parent">The control requesting help. The shared implementation does not use the owner directly.</param>
    /// <param name="url">The local file path or absolute URL to open.</param>
    public static void ShowHelp(Control? parent, string? url)
        => ShowHelp(parent, url, HelpNavigator.TableOfContents, null);

    /// <summary>
    /// Displays the contents of the help file or URL using the requested navigation mode.
    /// </summary>
    /// <param name="parent">The control requesting help. The shared implementation does not use the owner directly.</param>
    /// <param name="url">The local file path or absolute URL to open.</param>
    /// <param name="navigator">The navigation mode requested by the caller.</param>
    public static void ShowHelp(Control? parent, string? url, HelpNavigator navigator)
        => ShowHelp(parent, url, navigator, null);

    /// <summary>
    /// Displays a help topic from the specified help file or URL.
    /// </summary>
    /// <param name="parent">The control requesting help. The shared implementation does not use the owner directly.</param>
    /// <param name="url">The local file path or absolute URL to open.</param>
    /// <param name="keyword">The topic keyword or fragment to navigate to.</param>
    public static void ShowHelp(Control? parent, string? url, string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            ShowHelp(parent, url, HelpNavigator.TableOfContents, null);
        else
            ShowHelp(parent, url, HelpNavigator.Topic, keyword);
    }

    /// <summary>
    /// Displays the contents of the help file or URL using the requested navigation mode and parameter.
    /// </summary>
    /// <param name="parent">The control requesting help. The shared implementation does not use the owner directly.</param>
    /// <param name="url">The local file path or absolute URL to open.</param>
    /// <param name="command">The navigation mode requested by the caller.</param>
    /// <param name="parameter">A navigation keyword, topic identifier, or backend-specific parameter.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> cannot be resolved.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the platform cannot open the resolved help target.</exception>
    public static void ShowHelp(Control? parent, string? url, HelpNavigator command, object? parameter)
    {
        Uri resolvedUri = Resolve(url) ?? throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, SR.HelpInvalidURL, url), nameof(url));
        OpenUri(ApplyNavigation(resolvedUri, command, parameter));
    }

    /// <summary>
    /// Displays the index of the specified help file or URL.
    /// </summary>
    /// <param name="parent">The control requesting help. The shared implementation does not use the owner directly.</param>
    /// <param name="url">The local file path or absolute URL to open.</param>
    public static void ShowHelpIndex(Control? parent, string? url)
        => ShowHelp(parent, url, HelpNavigator.Index, null);

    /// <summary>
    /// Requests a short contextual help popup.
    /// </summary>
    /// <param name="parent">The control requesting the popup.</param>
    /// <param name="caption">The text to display.</param>
    /// <param name="location">The desired popup location in screen coordinates.</param>
    /// <remarks>
    /// The current shared framework does not yet have a platform-neutral popup help surface. The
    /// request is kept as a compatible API and is reported through diagnostics so a backend or
    /// application-level help service can provide a richer implementation later.
    /// </remarks>
    public static void ShowPopup(Control? parent, string caption, Point location)
    {
        ArgumentNullException.ThrowIfNull(caption);

        if (caption.Length == 0)
            return;

        Debug.WriteLine($"Help popup requested at {location}: {caption}");
    }

    private static Uri ApplyNavigation(Uri uri, HelpNavigator command, object? parameter)
    {
        string? fragment = command switch
        {
            HelpNavigator.Topic or HelpNavigator.AssociateIndex or HelpNavigator.KeywordIndex => parameter as string,
            HelpNavigator.TopicId => parameter switch
            {
                int topicId => topicId.ToString(CultureInfo.InvariantCulture),
                string topicIdText => topicIdText,
                _ => null
            },
            _ => null
        };

        if (string.IsNullOrWhiteSpace(fragment))
            return uri;

        var builder = new UriBuilder(uri)
        {
            Fragment = fragment.TrimStart('#')
        };

        return builder.Uri;
    }

    private static void OpenUri(Uri uri)
    {
        string target = uri.IsFile ? uri.LocalPath : uri.AbsoluteUri;

        try
        {
            using var _ = Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, SR.HelpUnableToLaunch, target), ex);
        }
    }

    private static Uri? Resolve(string? partialUri)
    {
        if (string.IsNullOrWhiteSpace(partialUri))
            return null;

        if (Uri.TryCreate(partialUri, UriKind.Absolute, out Uri? absoluteUri))
            return absoluteUri;

        try
        {
            string candidate = System.IO.Path.GetFullPath(partialUri, AppContext.BaseDirectory);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return new Uri(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return Uri.TryCreate(new Uri(AppContext.BaseDirectory), partialUri, out Uri? relativeUri)
            ? relativeUri
            : null;
    }
}
