using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext
{
    internal static class PrintingPlatformConversions
    {
        public static PlatformPrinterSettings ToPlatformPrinterSettings(PrinterSettings settings)
        {
            return new PlatformPrinterSettings
            {
                CanDuplex = settings.CanDuplex,
                Collate = settings.Collate,
                Copies = settings.Copies,
                Duplex = (int)settings.Duplex,
                FromPage = settings.FromPage,
                IsPlotter = settings.IsPlotter,
                LandscapeAngle = settings.LandscapeAngle,
                MaximumCopies = settings.MaximumCopies,
                MaximumPage = settings.MaximumPage,
                MinimumPage = settings.MinimumPage,
                PrintFileName = settings.PrintFileName,
                PrintRange = ToPlatformPrintRange(settings.PrintRange),
                PrintToFile = settings.PrintToFile,
                PrinterName = settings.PrinterName,
                SupportsColor = settings.SupportsColor,
                ToPage = settings.ToPage
            };
        }

        public static void ApplyPlatformPrinterSettings(PlatformPrinterSettings source, PrinterSettings target)
        {
            target.CanDuplex = source.CanDuplex;
            target.Collate = source.Collate;
            target.Copies = (short)Math.Max(1, (int)source.Copies);
            target.Duplex = Enum.IsDefined(typeof(Duplex), source.Duplex) ? (Duplex)source.Duplex : Duplex.Default;
            target.FromPage = Math.Max(0, source.FromPage);
            target.IsPlotter = source.IsPlotter;
            target.LandscapeAngle = source.LandscapeAngle;
            target.MaximumCopies = Math.Max(1, source.MaximumCopies);
            target.MaximumPage = Math.Max(0, source.MaximumPage);
            target.MinimumPage = Math.Max(0, source.MinimumPage);
            target.PrintFileName = source.PrintFileName;
            target.PrintRange = FromPlatformPrintRange(source.PrintRange);
            target.PrintToFile = source.PrintToFile;
            target.PrinterName = source.PrinterName;
            target.SupportsColor = source.SupportsColor;
            target.ToPage = Math.Max(target.FromPage, source.ToPage);
        }

        public static PlatformPageSettings ToPlatformPageSettings(PageSettings settings)
        {
            return new PlatformPageSettings
            {
                Color = settings.Color,
                Landscape = settings.Landscape,
                Margins = ToPlatformMargins(settings.Margins),
                PaperSize = new PlatformPaperSize
                {
                    Kind = (int)settings.PaperSize.Kind,
                    Name = settings.PaperSize.PaperName,
                    Width = settings.PaperSize.Width,
                    Height = settings.PaperSize.Height
                },
                PaperSource = new PlatformPaperSource
                {
                    Kind = (int)settings.PaperSource.Kind,
                    Name = settings.PaperSource.SourceName
                }
            };
        }

        public static void ApplyPlatformPageSettings(PlatformPageSettings source, PageSettings target)
        {
            target.Color = source.Color;
            target.Landscape = source.Landscape;
            target.Margins = FromPlatformMargins(source.Margins);
            target.PaperSize = new PaperSize(source.PaperSize.Name, source.PaperSize.Width, source.PaperSize.Height)
            {
                Kind = Enum.IsDefined(typeof(PaperKind), source.PaperSize.Kind)
                    ? (PaperKind)source.PaperSize.Kind
                    : PaperKind.Custom
            };
            target.PaperSource = new PaperSource
            {
                Kind = Enum.IsDefined(typeof(PaperSourceKind), source.PaperSource.Kind)
                    ? (PaperSourceKind)source.PaperSource.Kind
                    : PaperSourceKind.Custom,
                SourceName = source.PaperSource.Name
            };
        }

        public static PlatformMargins ToPlatformMargins(Margins margins)
        {
            return new PlatformMargins
            {
                Left = margins.Left,
                Right = margins.Right,
                Top = margins.Top,
                Bottom = margins.Bottom
            };
        }

        public static Margins FromPlatformMargins(PlatformMargins margins)
            => new Margins(margins.Left, margins.Right, margins.Top, margins.Bottom);

        private static PlatformPrintRange ToPlatformPrintRange(PrintRange value)
        {
            return value switch
            {
                PrintRange.Selection => PlatformPrintRange.Selection,
                PrintRange.SomePages => PlatformPrintRange.SomePages,
                PrintRange.CurrentPage => PlatformPrintRange.CurrentPage,
                _ => PlatformPrintRange.AllPages
            };
        }

        private static PrintRange FromPlatformPrintRange(PlatformPrintRange value)
        {
            return value switch
            {
                PlatformPrintRange.Selection => PrintRange.Selection,
                PlatformPrintRange.SomePages => PrintRange.SomePages,
                PlatformPrintRange.CurrentPage => PrintRange.CurrentPage,
                _ => PrintRange.AllPages
            };
        }
    }
}
