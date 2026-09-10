using Android.Content;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

internal static class AndroidIntentActivation
{
    internal static PlatformApplicationActivation Read(Intent? intent)
    {
        var streams = new List<string>();
        if (intent?.Action is Intent.ActionSend or Intent.ActionSendMultiple)
        {
            if (intent.ClipData is { } clip)
            {
                if (clip.ItemCount > PlatformApplicationActivation.MaximumItemCount)
                    throw new InvalidOperationException("Too many activation items.");
                for (int i = 0; i < clip.ItemCount; i++)
                    if (clip.GetItemAt(i)?.Uri?.ToString() is { } uri) streams.Add(uri);
            }
            // API 23-32 require the original Parcelable accessors; read only URI payloads.
#pragma warning disable CS0618, CA1422
            if (intent.GetParcelableExtra(Intent.ExtraStream) is global::Android.Net.Uri single && single.ToString() is { } singleValue)
                streams.Add(singleValue);
            if (intent.Action == Intent.ActionSendMultiple && intent.GetParcelableArrayListExtra(Intent.ExtraStream) is { } many)
            {
                if (many.Count > PlatformApplicationActivation.MaximumItemCount)
                    throw new InvalidOperationException("Too many activation items.");
                foreach (var item in many)
                    if (item is global::Android.Net.Uri uri && uri.ToString() is { } value) streams.Add(value);
            }
#pragma warning restore CS0618, CA1422
        }
        return AndroidActivationMapper.Create(intent?.DataString, streams);
    }
}
