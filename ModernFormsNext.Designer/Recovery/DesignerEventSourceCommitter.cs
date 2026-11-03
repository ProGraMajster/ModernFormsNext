namespace ModernFormsNext.Designer.Recovery;

// File replacement has no compare-and-swap API. Preserve the exact replaced file atomically,
// then verify it against the expected snapshot so even a save in the last comparison/write gap
// cannot discard user text. Conflict recovery copies are intentionally retained, never cleaned
// as ordinary temporary output or automatically chosen as the user's final source version.
internal sealed class DesignerEventSourceCommitter(byte[] expectedBytes) : IDesignerAtomicFileCommitter
{
    public void Commit(string temporaryPath, string destinationPath)
    {
        var directory = IOPath.GetDirectoryName(destinationPath)!;
        var before = IOPath.Combine(directory, $".mfn-event-original-{Guid.NewGuid():N}.tmp");
        File.Replace(temporaryPath, destinationPath, before, ignoreMetadataErrors: true);
        bool matches;
        try { matches = Matches(before, expectedBytes); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not verify replaced event source. Its original bytes are retained at '{before}'. "
                + "Review that recovery file before editing again; no binding was committed.", ex);
        }
        if (matches)
        {
            File.Delete(before);
            return;
        }

        var conflict = IOPath.Combine(directory, $".mfn-event-conflict-{Guid.NewGuid():N}.tmp");
        try
        {
            // Restore the version actually replaced. A further external save during recovery is
            // itself retained atomically in conflict, so all versions survive for explicit review.
            File.Replace(before, destinationPath, conflict, ignoreMetadataErrors: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Event source changed concurrently. Its replaced version is retained at '{before}'. "
                + $"Recovery may also have retained '{conflict}'. Review these files before editing again.", ex);
        }
        throw new IOException($"Event source changed concurrently; the replaced user version was restored. "
            + $"Another version was retained at '{conflict}'. Review it before editing again. No binding was committed.");
    }

    private static bool Matches(string path, byte[] expected)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != expected.Length) return false;
        Span<byte> buffer = stackalloc byte[4096];
        var offset = 0;
        while (offset < expected.Length)
        {
            var count = stream.Read(buffer[..Math.Min(buffer.Length, expected.Length - offset)]);
            if (count == 0 || !buffer[..count].SequenceEqual(expected.AsSpan(offset, count))) return false;
            offset += count;
        }
        return true;
    }
}
