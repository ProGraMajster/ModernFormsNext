using Android.App;
using Android.Views;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.CrossPlatform.Sample;

// Instrumentation-only native lookup. Production application startup needs only Application.Run.
internal static class NativeValidationViews
{
    internal static AndroidSkiaHostView Main(Activity activity) =>
        Find(activity.FindViewById<ViewGroup>(global::Android.Resource.Id.Content)!).First();

    internal static IEnumerable<AndroidSkiaHostView> Find(View view)
    {
        if (view is AndroidSkiaHostView surface) yield return surface;
        if (view is ViewGroup group)
            for (int i = 0; i < group.ChildCount; i++)
                foreach (var child in Find(group.GetChildAt(i)!)) yield return child;
    }
}
