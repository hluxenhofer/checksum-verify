using System.Text.RegularExpressions;

namespace ChecksumVerify.Core;

/// <param name="IsAlgorithmCertain">True when the algorithm comes from a BSD tag or the file extension,
/// false when it was only inferred from the hash length (so 64 chars is "probably SHA-256").</param>
public sealed record ParsedHash(string Hash, HashAlgorithmId? Algorithm, bool IsAlgorithmCertain = false);

public static partial class HashFileParser
{
    // BSD style (sha256sum --tag): "SHA256 (file name) = <hash>"
    [GeneratedRegex(@"^(?<algo>[A-Za-z0-9\-]+) \((?<name>.+)\) = (?<hash>[0-9a-fA-F]+)$")]
    private static partial Regex BsdLinePattern();

    /// <summary>
    /// Finds the expected hash for <paramref name="targetFileName"/> in a hash file.
    /// Returns null when the file has no entry for the target.
    /// The returned algorithm is taken from (in order): the BSD tag, the file extension,
    /// the hash length. It is null only when none of these is conclusive.
    /// </summary>
    public static ParsedHash? TryParse(string hashFilePath, string targetFileName)
    {
        var extension = Path.GetExtension(hashFilePath).ToLowerInvariant();
        var extensionAlgorithm = AlgorithmForHashFile(hashFilePath);
        var isSidecar = AlgorithmFromExtension(extension) is not null;

        foreach (var rawLine in File.ReadLines(hashFilePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Sidecar files may contain just the hash
            if (isSidecar && ClipboardDetector.IsHexString(line))
                return Resolve(line, null, extensionAlgorithm);

            if (TryParseLine(line, out var hash, out var name, out var tagAlgorithm)
                && string.Equals(name, targetFileName, StringComparison.OrdinalIgnoreCase))
                return Resolve(hash, tagAlgorithm, extensionAlgorithm);
        }

        return null;
    }

    private static ParsedHash Resolve(string hash, HashAlgorithmId? tagAlgorithm, HashAlgorithmId? extensionAlgorithm)
    {
        var algorithm = tagAlgorithm ?? extensionAlgorithm;
        var certain = algorithm is not null;
        // BLAKE2b has no fixed length (b2sum defaults to 512 bits); the hash length decides
        if (algorithm is HashAlgorithmId.Blake2b_256 or HashAlgorithmId.Blake2b_512)
            algorithm = hash.Length == 64 ? HashAlgorithmId.Blake2b_256 : HashAlgorithmId.Blake2b_512;
        algorithm ??= ClipboardDetector.SuggestAlgorithm(hash);
        return new ParsedHash(hash, algorithm, certain);
    }

    /// <summary>Algorithm implied by a sidecar extension (.sha256, .b3, ...).</summary>
    public static HashAlgorithmId? AlgorithmFromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".sha256"   => HashAlgorithmId.Sha256,
            ".sha512"   => HashAlgorithmId.Sha512,
            ".sha3-256" => HashAlgorithmId.Sha3_256,
            ".sha3-512" => HashAlgorithmId.Sha3_512,
            // b2sum defaults to 512 bits; TryParse narrows to 256 by hash length
            ".b2" or ".blake2" or ".blake2b" => HashAlgorithmId.Blake2b_512,
            ".b3" or ".blake3"               => HashAlgorithmId.Blake3,
            ".md5"  => HashAlgorithmId.Md5,
            ".sha1" => HashAlgorithmId.Sha1,
            _       => null
        };

    /// <summary>Algorithm implied by a hash file's name: sidecar extensions plus .sha256sum/.sha512sum lists.</summary>
    public static HashAlgorithmId? AlgorithmForHashFile(string hashFilePath)
    {
        var extension = Path.GetExtension(hashFilePath).ToLowerInvariant();
        return AlgorithmFromExtension(extension) ?? extension switch
        {
            ".sha256sum" => HashAlgorithmId.Sha256,
            ".sha512sum" => HashAlgorithmId.Sha512,
            _ => null
        };
    }

    private static bool TryParseLine(string line, out string hash, out string name, out HashAlgorithmId? tagAlgorithm)
    {
        hash = name = string.Empty;
        tagAlgorithm = null;

        var bsd = BsdLinePattern().Match(line);
        if (bsd.Success)
        {
            tagAlgorithm = AlgorithmFromTag(bsd.Groups["algo"].Value);
            hash = bsd.Groups["hash"].Value;
            name = Unescape(bsd.Groups["name"].Value, escaped: false);
            return true;
        }

        // GNU coreutils: a leading backslash marks an escaped file name
        var escaped = line.StartsWith('\\');
        var body = escaped ? line[1..] : line;

        // "<hash>  <filename>" (text mode) or "<hash> *<filename>" (binary mode)
        var separator = body.IndexOf(' ');
        if (separator <= 0 || separator + 2 > body.Length) return false;
        var mode = body[separator + 1];
        if (mode is not (' ' or '*')) return false;

        var candidate = body[..separator];
        if (!ClipboardDetector.IsHexString(candidate)) return false;

        hash = candidate;
        name = Unescape(body[(separator + 2)..], escaped);
        return true;
    }

    private static string Unescape(string name, bool escaped)
    {
        if (!escaped) return name;
        return name.Replace("\\n", "\n").Replace("\\\\", "\\");
    }

    private static HashAlgorithmId? AlgorithmFromTag(string tag) =>
        tag.ToUpperInvariant() switch
        {
            "SHA256" or "SHA-256"         => HashAlgorithmId.Sha256,
            "SHA512" or "SHA-512"         => HashAlgorithmId.Sha512,
            "SHA3-256"                    => HashAlgorithmId.Sha3_256,
            "SHA3-512"                    => HashAlgorithmId.Sha3_512,
            "SHA1" or "SHA-1"             => HashAlgorithmId.Sha1,
            "MD5"                         => HashAlgorithmId.Md5,
            "BLAKE3"                      => HashAlgorithmId.Blake3,
            "BLAKE2B" or "BLAKE2B-512"    => HashAlgorithmId.Blake2b_512,
            "BLAKE2B-256"                 => HashAlgorithmId.Blake2b_256,
            _ => null
        };
}
