using System.Security.Cryptography;
using System.Text;

namespace ModernFormsNext.Designer.Recovery;

/// <summary>
/// Computes content fingerprints used by recovery integrity checks and external-file tracking.
/// </summary>
internal static class DesignerFileHash
{
    public const int Sha256HexLength = 64;

    public static string ComputeFileSha256(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Replacement-based writers need to be able to rename over the file while a fingerprint
        // is being read. The open handle still observes one complete pre- or post-replacement file.
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        return ToLowerHex(SHA256.HashData(stream));
    }

    public static string ComputeUtf8Sha256(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ToLowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public static bool EqualsSha256(string content, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!IsSha256(expectedHash))
            return false;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        byte[] expected;

        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool IsSha256(string? value)
        => value is { Length: Sha256HexLength }
        && value.All(Uri.IsHexDigit);

    private static string ToLowerHex(ReadOnlySpan<byte> hash)
        => Convert.ToHexString(hash).ToLowerInvariant();
}
