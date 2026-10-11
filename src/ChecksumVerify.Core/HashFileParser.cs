namespace ChecksumVerify.Core;

public sealed record ParsedHash(string Hash, HashAlgorithmId? Algorithm);

public static class HashFileParser
{
    public static ParsedHash? TryParse(string hashFilePath, string targetFileName)
    {
        var extension = Path.GetExtension(hashFilePath).ToLowerInvariant();
        var algorithm = AlgorithmFromExtension(extension);
        var lines = File.ReadAllLines(hashFilePath);

        // Sidecar: single-hash file — the hash is the first non-empty line
        if (algorithm is not null && IsSidecarExtension(extension))
        {
            var bare = lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            if (bare is null) return null;

            // Could be a bare hex string or a GNU line with the filename
            var gnuParsed = TryParseGnuLine(bare, targetFileName);
            return gnuParsed is not null
                ? new ParsedHash(gnuParsed, algorithm)
                : new ParsedHash(bare.Split(' ')[0].Trim(), algorithm);
        }

        // GNU coreutils format: "<hash>  <filename>" or "<hash> *<filename>"
        var gnuAlgorithm = algorithm ?? AlgorithmFromGnuExtension(extension);
        foreach (var line in lines)
        {
            var hash = TryParseGnuLine(line, targetFileName);
            if (hash is not null)
                return new ParsedHash(hash, gnuAlgorithm);
        }

        return null;
    }

    public static HashAlgorithmId? AlgorithmFromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".sha256"   => HashAlgorithmId.Sha256,
            ".sha512"   => HashAlgorithmId.Sha512,
            ".sha3-256" => HashAlgorithmId.Sha3_256,
            ".sha3-512" => HashAlgorithmId.Sha3_512,
            ".b2" or ".blake2" or ".blake2b" => HashAlgorithmId.Blake2b_256,
            ".b3" or ".blake3"               => HashAlgorithmId.Blake3,
            ".md5"  => HashAlgorithmId.Md5,
            ".sha1" => HashAlgorithmId.Sha1,
            _       => null
        };

    private static bool IsSidecarExtension(string extension) =>
        AlgorithmFromExtension(extension) is not null;

    private static HashAlgorithmId? AlgorithmFromGnuExtension(string extension) =>
        extension switch
        {
            ".sha256sum" => HashAlgorithmId.Sha256,
            ".sha512sum" => HashAlgorithmId.Sha512,
            _ => null
        };

    private static string? TryParseGnuLine(string line, string targetFileName)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) return null;

        // Binary mode: "<hash> *<filename>"
        var binaryIdx = trimmed.IndexOf(" *", StringComparison.Ordinal);
        if (binaryIdx >= 0)
        {
            var hashPart = trimmed[..binaryIdx];
            var filePart = trimmed[(binaryIdx + 2)..];
            return string.Equals(filePart, targetFileName, StringComparison.OrdinalIgnoreCase)
                ? hashPart : null;
        }

        // Text mode: "<hash>  <filename>" (two spaces)
        var textIdx = trimmed.IndexOf("  ", StringComparison.Ordinal);
        if (textIdx >= 0)
        {
            var hashPart = trimmed[..textIdx];
            var filePart = trimmed[(textIdx + 2)..];
            return string.Equals(filePart, targetFileName, StringComparison.OrdinalIgnoreCase)
                ? hashPart : null;
        }

        return null;
    }
}
