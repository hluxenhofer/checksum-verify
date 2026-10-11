using System.Text.RegularExpressions;

namespace ChecksumVerify.Core;

public sealed record DetectedHash(string Hash, HashAlgorithmId? SuggestedAlgorithm);

public static partial class ClipboardDetector
{
    [GeneratedRegex(@"^[0-9a-fA-F]+$")]
    private static partial Regex HexPattern();

    private static readonly Dictionary<string, HashAlgorithmId> PrefixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sha256"]   = HashAlgorithmId.Sha256,
        ["sha-256"]  = HashAlgorithmId.Sha256,
        ["sha512"]   = HashAlgorithmId.Sha512,
        ["sha-512"]  = HashAlgorithmId.Sha512,
        ["sha3-256"] = HashAlgorithmId.Sha3_256,
        ["sha3-512"] = HashAlgorithmId.Sha3_512,
        ["sha1"]     = HashAlgorithmId.Sha1,
        ["sha-1"]    = HashAlgorithmId.Sha1,
        ["md5"]      = HashAlgorithmId.Md5,
        ["blake3"]   = HashAlgorithmId.Blake3,
        ["b3"]       = HashAlgorithmId.Blake3,
        ["blake2b"]  = HashAlgorithmId.Blake2b_512,
        ["b2"]       = HashAlgorithmId.Blake2b_512,
    };

    public static bool IsHexString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        return trimmed.Length > 0 && HexPattern().IsMatch(trimmed);
    }

    public static HashAlgorithmId? SuggestAlgorithm(string hexString) =>
        hexString.Length switch
        {
            32  => HashAlgorithmId.Md5,
            40  => HashAlgorithmId.Sha1,
            64  => HashAlgorithmId.Sha256,
            128 => HashAlgorithmId.Sha512,
            _   => null
        };

    /// <summary>
    /// Strips an optional "algo:" prefix (e.g. "sha256:abc123") and returns the hex portion.
    /// Returns the original trimmed string unchanged when no recognized prefix is present.
    /// </summary>
    public static string StripAlgoPrefix(string text)
    {
        var trimmed = text.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx > 0)
        {
            var prefix = trimmed[..colonIdx];
            var hex = trimmed[(colonIdx + 1)..].Trim();
            if (PrefixMap.ContainsKey(prefix) && IsHexString(hex))
                return hex;
        }
        return trimmed;
    }

    public static DetectedHash? TryDetect(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText)) return null;
        var trimmed = clipboardText.Trim();

        // Handle "algo:hex" format (e.g. "sha256:5906b15f...")
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx > 0)
        {
            var prefix = trimmed[..colonIdx];
            var hex = trimmed[(colonIdx + 1)..].Trim();
            if (PrefixMap.TryGetValue(prefix, out var algoFromPrefix) && IsHexString(hex))
                return new DetectedHash(hex.ToLowerInvariant(), algoFromPrefix);
        }

        if (!IsHexString(trimmed)) return null;
        var hash = trimmed.ToLowerInvariant();
        return new DetectedHash(hash, SuggestAlgorithm(hash));
    }
}
