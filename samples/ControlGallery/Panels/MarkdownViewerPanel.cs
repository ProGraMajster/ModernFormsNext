using System;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    /// <summary>
    /// Demonstrates the MarkdownViewer control and the shared document rendering pipeline.
    /// </summary>
    public class MarkdownViewerPanel : Panel
    {
        private const string SampleMarkdown = """"
            # Heading 1

            ## Heading 2

            Normal paragraph with **bold**, *italic* and ***bold italic***.

            Drag to select this text. Press Ctrl+C to copy it.

            ## Link interaction

            Normal link: [ModernFormsNext repository](https://github.com/ProGraMajster/ModernFormsNext).

            Wrapped link: [This deliberately long link label should wrap across at least two visual
            lines so both portions can be clicked and tested independently](https://example.com/wrapped-link).

            - Link inside a list: [list documentation](https://example.com/list-link)

            | Link location | Target |
            | --- | --- |
            | Table cell | [table documentation](https://example.com/table-link) |

            😀 [Link after emoji](https://example.com/emoji-link)

            This deliberately long paragraph wraps across several visual lines so selection can be
            checked while dragging forward and backward through a single RichTextKit text block.
            It also contains a [wrapped link with enough visible text to continue onto another line and
            verify link hit testing](https://example.com/wrapped-link) inside selectable content.

            `inline code`

            A [link](https://github.com/ProGraMajster/ModernFormsNext).
            Auto links: https://example.com and <hello@example.com>.

            > Block quote

            - first level
            - second item
              - second level
                - third level

            - [x] Native Markdown rendering
            - [x] Document model
            - [ ] Markdown editor

            1. ordered item
            2. second ordered item

            98. wide marker item
            99. another wide marker item
            100. wrapping ordered item with a long body that exercises the hanging indent column

            | Description | Status |
            | :--- | :---: |
            | A significantly longer description that should receive more horizontal space without manually configured widths | Ready |
            | Short | OK |

            ![Local ControlGallery image](Images/icon.png "Local image")

            ![Missing image fallback](Images/does-not-exist.png "Fallback")

            ---

            ```csharp
            var button = new Button
            {
                Text = "ModernFormsNext"
            };
            ```

            ```json
            {
                "framework": "ModernFormsNext",
                "renderer": "SkiaSharp"
            }
            ```

            ```powershell
            $framework = "ModernFormsNext"
            if ($framework) { Write-Output $framework } # highlighted shell family
            ```

            ```unknown-language
            Unknown languages keep plain code without changing the source text.
            ```

            ```
            Plain fenced code without a language identifier.
            ```

            This final paragraph intentionally makes the document long enough to exercise vertical
            scrolling in the shared DocumentViewer layout cache. Links, images, lists, quotes,
            headings, tables, footnotes and code blocks all travel through the same document
            pipeline.[^pipeline]

            [^pipeline]: Footnotes are converted to native document nodes and rendered after the
                main content without using HTML.
            """";

        public MarkdownViewerPanel()
        {
            Padding = new Padding(20);

            var output = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                Text = "Last link clicked: none",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var viewer = new MarkdownViewer
            {
                Dock = DockStyle.Fill
            };

            viewer.ImageLoadOptions.MaxDownloadBytes = 2 * 1024 * 1024;
            viewer.ImageLoadOptions.MaxDecodedPixels = 8_000_000;
            viewer.ImageLoadOptions.RequestTimeout = TimeSpan.FromSeconds(5);
            viewer.DocumentStyle.ShowCodeBlockLanguage = true;
            viewer.Markdown = SampleMarkdown;

            viewer.LinkClicked += (_, e) =>
            {
                output.Text = "Last link clicked: " + e.Destination;
            };

            Controls.Add(output);
            Controls.Add(viewer);
        }
    }
}
