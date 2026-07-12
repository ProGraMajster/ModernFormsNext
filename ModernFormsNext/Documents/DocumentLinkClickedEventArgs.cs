using System;

namespace ModernFormsNext.Documents;

/// <summary>
/// Provides data for the <see cref="ModernFormsNext.DocumentViewer.LinkClicked"/> event used by
/// document-based viewers.
/// </summary>
public sealed class DocumentLinkClickedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new <see cref="DocumentLinkClickedEventArgs"/> instance.
    /// </summary>
    /// <param name="destination">The clicked link destination.</param>
    /// <param name="text">The visible text associated with the link.</param>
    /// <param name="title">The optional link title.</param>
    /// <param name="button">The mouse button used to activate the link.</param>
    public DocumentLinkClickedEventArgs(string destination, string text, string? title, MouseButtons button)
    {
        Destination = destination;
        Text = text;
        Title = title;
        Button = button;
    }

    /// <summary>
    /// Gets the mouse button used to activate the link.
    /// </summary>
    public MouseButtons Button { get; }

    /// <summary>
    /// Gets the clicked link destination.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Gets the optional link title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the visible text associated with the link.
    /// </summary>
    public string Text { get; }
}
