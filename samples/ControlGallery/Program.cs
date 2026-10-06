using System;
using ModernFormsNext;

namespace ControlGallery
{
    public class Program
    {
        [STAThread]
        static void Main (string[] args)
        {
            // Auto is the default and currently selects Software. Configure before creating
            // a Form; use --software for explicit CPU rendering and diagnostic comparisons.
            Application.ConfigureRendering(new RenderingOptions {
                Backend = Array.IndexOf(args, "--software") >= 0 ? RenderingBackend.Software : RenderingBackend.Auto
            });
            Application.Run (new MainForm ());
        }
    }
}
