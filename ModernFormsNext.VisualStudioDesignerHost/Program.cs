using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.VisualStudioDesignerHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        DesignerHostDiagnosticLog.Write("START");

        try
        {
            RegisterGlobalExceptionDiagnostics();

            var arguments = DesignerHostArguments.Parse(args);
            DesignerHostDiagnosticLog.Write(
                $"ARGS_PARSED DesignFile={arguments.DesignDocumentPath ?? "<none>"} " +
                $"Project={arguments.ProjectPath ?? "<none>"} Pipe={arguments.PipeName ?? "<none>"} " +
                $"HostMode={arguments.HostingMode} OwnerProcessId={arguments.OwnerProcessId} " +
                $"ParentWindow=0x{arguments.ParentWindowHandle.ToInt64():X}");

            // Form construction also ensures the backend is initialized, but doing it explicitly
            // here makes failures in process DPI/backend setup distinguishable from failures in the
            // form constructor. EnsureInitialized is idempotent, so Application.Run remains the
            // authoritative application-lifetime entry point.
            DesignerHostDiagnosticLog.Write("DPI_SETUP_BEGIN");
            FrameworkBootstrap.EnsureInitialized();
            DesignerHostDiagnosticLog.Write("DPI_SETUP_OK");

            DesignerHostDiagnosticLog.Write("FORM_CONSTRUCTOR_BEGIN");
            var form = new VisualStudioDesignerHostForm(arguments);
            DesignerHostDiagnosticLog.Write("FORM_CONSTRUCTOR_OK");

            DesignerHostDiagnosticLog.Write("APPLICATION_RUN_BEGIN");
            Application.Run(form);
            DesignerHostDiagnosticLog.Write("APPLICATION_RUN_END");
        }
        catch (Exception ex)
        {
            DesignerHostDiagnosticLog.WriteException("MAIN_EXCEPTION", ex);
            Environment.ExitCode = -1;
        }
    }

    private static void RegisterGlobalExceptionDiagnostics()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                DesignerHostDiagnosticLog.WriteException(
                    $"APPDOMAIN_UNHANDLED_EXCEPTION IsTerminating={eventArgs.IsTerminating}",
                    exception);
            }
            else
            {
                DesignerHostDiagnosticLog.Write(
                    $"APPDOMAIN_UNHANDLED_EXCEPTION IsTerminating={eventArgs.IsTerminating}{Environment.NewLine}" +
                    $"ExceptionObjectType={eventArgs.ExceptionObject?.GetType().FullName ?? "<null>"}{Environment.NewLine}" +
                    $"ExceptionObject={eventArgs.ExceptionObject ?? "<null>"}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            DesignerHostDiagnosticLog.WriteException(
                "TASKSCHEDULER_UNOBSERVED_TASK_EXCEPTION",
                eventArgs.Exception);

        // ModernFormsNext owns its dispatcher and native WndProc; it does not use
        // System.Windows.Forms.Application and therefore has no WinForms ThreadException event.
        // Exceptions escaping a native callback are reported by the AppDomain handler above.
        DesignerHostDiagnosticLog.Write("GLOBAL_EXCEPTION_DIAGNOSTICS_READY");
    }
}
