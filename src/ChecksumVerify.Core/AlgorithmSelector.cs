namespace ChecksumVerify.Core;

/// <summary>
/// Decides which algorithm the dropdown should show for a detected hash.
/// A hash length is ambiguous (64 hex chars: SHA-256, SHA3-256, BLAKE3, BLAKE2b-256), so detection
/// only decides when it has to.
/// </summary>
public static class AlgorithmSelector
{
    /// <summary>
    /// Returns the algorithm to select, or null to leave the current selection alone.
    /// </summary>
    /// <param name="detected">The hash found in the input or clipboard.</param>
    /// <param name="current">The algorithm currently selected.</param>
    /// <param name="userChoice">The algorithm the user picked by hand, if any.</param>
    public static HashAlgorithmId? Choose(DetectedHash detected, HashAlgorithmId current, HashAlgorithmId? userChoice)
    {
        if (detected.SuggestedAlgorithm is not { } suggested) return null;

        // "blake3:…" is an explicit statement, stronger than any earlier choice
        if (detected.IsExplicit) return suggested;

        var length = detected.Hash.Length;
        if (userChoice is { } choice && HashEngine.HexLength(choice) == length) return choice;
        if (HashEngine.HexLength(current) == length) return null;
        return suggested;
    }

    /// <summary>
    /// Text for the note under the hash field when the hash length fits several algorithms;
    /// null when there is nothing to disambiguate (explicit prefix, unique length, no hash).
    /// </summary>
    public static string? DescribeAmbiguity(
        DetectedHash? detected,
        HashAlgorithmId selected,
        Func<HashAlgorithmId, bool>? isSupported = null)
    {
        if (detected is null or { IsExplicit: true }) return null;

        isSupported ??= HashEngine.IsSupported;
        var length = detected.Hash.Length;
        var candidates = Enum.GetValues<HashAlgorithmId>()
            .Where(a => HashEngine.HexLength(a) == length && isSupported(a))
            .ToArray();
        if (candidates.Length < 2) return null;

        return $"A {length}-character hash could be {string.Join(", ", candidates.Select(a => a.DisplayName()))}. " +
               $"Selected: {selected.DisplayName()} — change the algorithm if the hash comes from another tool.";
    }
}
