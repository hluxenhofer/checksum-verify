namespace ChecksumVerify.Core;

/// <param name="Expected">The hash to compare against; null when <paramref name="Error"/> is set.</param>
/// <param name="Algorithm">The algorithm to hash with (may differ from the one selected, if a hash file dictates it).</param>
/// <param name="Error">What is wrong with the inputs, in words for the user; null when they are fine.</param>
public sealed record VerifyInput(string? Expected, HashAlgorithmId Algorithm, string? Error);

/// <summary>
/// Turns what the user entered into the expected hash and algorithm, or an error message.
/// Runs before the file is read, so problems show up at once even for multi-GB files.
/// </summary>
public static class VerifyInputResolver
{
    public static VerifyInput FromHash(string? text, HashAlgorithmId selected)
    {
        var expected = ClipboardDetector.StripAlgoPrefix(text ?? string.Empty);
        if (expected.Length == 0)
            return Fail(selected, "No hash provided. Enter or paste an expected hash first.");
        if (!ClipboardDetector.IsHexString(expected))
            return Fail(selected, "The expected hash must contain only hexadecimal characters (0-9, a-f).");
        return CheckLength(expected, selected);
    }

    /// <exception cref="IOException">The hash file cannot be read.</exception>
    public static VerifyInput FromHashFile(
        string? hashFilePath,
        string targetFileName,
        HashAlgorithmId selected,
        Func<HashAlgorithmId, bool>? isSupported = null)
    {
        isSupported ??= HashEngine.IsSupported;

        if (hashFilePath is null)
            return Fail(selected, "No hash file selected. Browse for a hash file first.");

        var parsed = HashFileParser.TryParse(hashFilePath, targetFileName);
        if (parsed is null)
            return Fail(selected, $"No entry found for {targetFileName} in the hash file.");

        var algorithm = selected;
        if (parsed.Algorithm is { } fromFile)
        {
            if (!isSupported(fromFile))
                return Fail(fromFile, $"{fromFile.DisplayName()} needs {HashAlgorithmNames.Sha3Requirement}.");

            // A tag or extension is authoritative; a guess from the length only wins when the current choice cannot fit
            if (parsed.IsAlgorithmCertain || HashEngine.HexLength(selected) != parsed.Hash.Length)
                algorithm = fromFile;
        }

        return CheckLength(parsed.Hash, algorithm);
    }

    private static VerifyInput CheckLength(string expected, HashAlgorithmId algorithm)
    {
        var needed = HashEngine.HexLength(algorithm);
        return expected.Length == needed
            ? new VerifyInput(expected, algorithm, null)
            : Fail(algorithm,
                $"The expected hash has {expected.Length} characters, but {algorithm.DisplayName()} produces {needed}. Check the selected algorithm.");
    }

    private static VerifyInput Fail(HashAlgorithmId algorithm, string error) => new(null, algorithm, error);
}
