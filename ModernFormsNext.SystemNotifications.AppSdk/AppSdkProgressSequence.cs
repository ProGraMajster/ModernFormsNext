using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ModernFormsNext.SystemNotifications.AppSdk;

// Used only for the SDK-only numeric-progress path (no explicit/package AUMID WinRT bridge).
// The SDK derives that identity from the executable path. Store only a counter under the same
// stable scope, never notification content. Reserve before sending, so crashes merely leave gaps.
internal static class AppSdkProgressSequence
{
    internal static Task<uint> NextAsync(CancellationToken cancellationToken, uint nativeFloor = 0) => Task.Run(async () =>
    {
        string executable = Path.GetFullPath(Environment.ProcessPath ?? throw new InvalidOperationException("No process path for notification identity."));
        string scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(executable.ToUpperInvariant())));
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModernFormsNext", "NotificationSequences");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, scope);
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        FileStream held;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); break; }
            catch (IOException e) when ((e.HResult & 0xffff) == 32 && timeout.Elapsed < TimeSpan.FromSeconds(5))
            { await Task.Delay(25, cancellationToken).ConfigureAwait(false); }
        }
        using (held)
        {
            uint previous = 0;
            if (File.Exists(path) && !uint.TryParse(File.ReadAllText(path), NumberStyles.None, CultureInfo.InvariantCulture, out previous))
                throw new IOException("The native progress sequence store is invalid; an older counter must not be reused.");
            previous = Math.Max(previous, nativeFloor);
            if (previous == uint.MaxValue) throw new IOException("The native progress sequence range is exhausted.");
            uint next = previous + 1;
            // Flush the replacement before atomic rename while holding the cross-process lock.
            // A crash before rename cannot follow a native submission, because reservation comes first.
            using (var pending = new FileStream(path + ".pending", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                pending.Write(Encoding.ASCII.GetBytes(next.ToString(CultureInfo.InvariantCulture)));
                pending.Flush(flushToDisk: true);
            }
            File.Move(path + ".pending", path, overwrite: true);
            return next;
        }
    }, cancellationToken);
}
