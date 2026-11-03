using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ModernFormsNext.VisualStudioExtension;

// One bounded code request travels over the existing per-pane pipe, polled off the VS UI thread.
// Source is data only. Paths are independently authorized against the pane's current design file.
internal sealed class DesignerCodeRequest
{
    internal const int MaximumTextCharacters = 512 * 1024;
    internal const int MaximumFrameCharacters = 4 * 1024 * 1024;
    internal const int MaximumInsertionCharacters = 16 * 1024;

    internal DesignerCodeRequest(string id, string operation, string designPath, string sourcePath,
        string expectedText, int offset, string insertion, long expiresUtcTicks)
    { Id = id; Operation = operation; DesignPath = designPath; SourcePath = sourcePath;
        ExpectedText = expectedText; Offset = offset; Insertion = insertion; ExpiresUtcTicks = expiresUtcTicks; }

    internal string Id { get; }
    internal string Operation { get; }
    internal string DesignPath { get; }
    internal string SourcePath { get; }
    internal string ExpectedText { get; }
    internal int Offset { get; }
    internal string Insertion { get; }
    internal long ExpiresUtcTicks { get; }

    internal string Encode() => string.Join("\t", "CODE", Id, Operation, Base64(DesignPath), Base64(SourcePath),
        Base64(ExpectedText), Offset.ToString(CultureInfo.InvariantCulture), Base64(Insertion), ExpiresUtcTicks.ToString(CultureInfo.InvariantCulture));

    internal static bool TryDecode(string? line, out DesignerCodeRequest request)
    {
        request = null!;
        if (line is null || line.Length > MaximumFrameCharacters) return false;
        var parts = line.Split('\t');
        if (parts.Length != 9 || parts[0] != "CODE" || !Guid.TryParseExact(parts[1], "N", out _)
            || parts[2] != "READ" && parts[2] != "INSERT" && parts[2] != "NAVIGATE"
            || !int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
            || !long.TryParse(parts[8], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)) return false;
        try
        {
            var design = Unbase64(parts[3]); var source = Unbase64(parts[4]);
            var text = Unbase64(parts[5]); var insertion = Unbase64(parts[7]);
            if (design.Length == 0 || design.Length > 32768 || source.Length == 0 || source.Length > 32768
                || text.Length > MaximumTextCharacters || insertion.Length > MaximumInsertionCharacters
                || offset < 0 || offset > text.Length || expires <= 0
                || parts[2] == "READ" && (text.Length != 0 || insertion.Length != 0 || offset != 0)
                || parts[2] == "NAVIGATE" && insertion.Length != 0) return false;
            request = new(parts[1], parts[2], design, source, text, offset, insertion, expires);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return false; }
    }

    internal bool IsAuthorized(string currentDesignPath)
    {
        if (DateTime.UtcNow.Ticks >= ExpiresUtcTicks || ExpiresUtcTicks - DateTime.UtcNow.Ticks > TimeSpan.FromSeconds(35).Ticks) return false;
        try
        {
            return string.Equals(Path.GetFullPath(DesignPath), Path.GetFullPath(currentDesignPath), StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFullPath(SourcePath), Path.ChangeExtension(Path.GetFullPath(currentDesignPath), ".cs"), StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetExtension(currentDesignPath), ".mfdesign", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return false; }
    }

    internal static string Result(string id, bool succeeded, string? text)
        => string.Join("\t", "CODE_RESULT", id, succeeded ? "OK" : "FAILED", Base64(text ?? string.Empty));

    internal static bool TryReadResult(string? line, string id, out bool succeeded, out string text)
    {
        succeeded = false; text = string.Empty;
        if (line is null || line.Length > MaximumFrameCharacters) return false;
        var parts = line.Split('\t');
        if (parts.Length != 4 || parts[0] != "CODE_RESULT" || parts[1] != id || parts[2] != "OK" && parts[2] != "FAILED") return false;
        try { text = Unbase64(parts[3]); succeeded = parts[2] == "OK"; return text.Length <= MaximumTextCharacters; }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return false; }
    }

    internal static async Task<string?> ReadBoundedLineAsync(TextReader reader)
    {
        var result = new StringBuilder();
        var buffer = new char[1024];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (count == 0) return null;
            var newline = Array.IndexOf(buffer, '\n', 0, count);
            var length = newline < 0 ? count : newline;
            if (result.Length + length > MaximumFrameCharacters) return null;
            result.Append(buffer, 0, length);
            if (newline >= 0) return result.ToString().TrimEnd('\r');
        }
    }

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private static string Unbase64(string text) => new UTF8Encoding(false, true).GetString(Convert.FromBase64String(text));
}
