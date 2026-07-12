using Android.Util;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal static class AndroidLogger
{
    internal static void Write(string message, Action<string>? diagnosticSink = null)
    {
        Log.Info(AndroidWindowKit.LogTag, message);
        diagnosticSink?.Invoke(message);
    }

    internal static void Error(string message, Exception exception, Action<string>? diagnosticSink = null)
    {
        var detail = $"{message} {exception}";
        Log.Error(AndroidWindowKit.LogTag, detail);
        diagnosticSink?.Invoke(detail);
    }
}
