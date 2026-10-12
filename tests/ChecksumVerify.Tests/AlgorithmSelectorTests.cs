using ChecksumVerify.Core;
using Xunit;

namespace ChecksumVerify.Tests;

public class AlgorithmSelectorTests
{
    private static DetectedHash Hash(int length, HashAlgorithmId? suggested, bool explicitPrefix = false) =>
        new(new string('a', length), suggested, explicitPrefix);

    [Fact]
    public void Choose_NoSuggestion_LeavesSelectionAlone()
    {
        Assert.Null(AlgorithmSelector.Choose(Hash(64, null), HashAlgorithmId.Sha512, null));
    }

    [Fact]
    public void Choose_ExplicitPrefix_AlwaysWins_EvenOverUserChoice()
    {
        var detected = Hash(64, HashAlgorithmId.Blake3, explicitPrefix: true);
        Assert.Equal(HashAlgorithmId.Blake3, AlgorithmSelector.Choose(detected, HashAlgorithmId.Sha256, HashAlgorithmId.Sha256));
    }

    [Fact]
    public void Choose_UserChoiceFitsLength_KeepsUserChoice()
    {
        // The user picked BLAKE3; a 64-char hash must not flip it back to SHA-256
        var chosen = AlgorithmSelector.Choose(Hash(64, HashAlgorithmId.Sha256), HashAlgorithmId.Sha1, HashAlgorithmId.Blake3);
        Assert.Equal(HashAlgorithmId.Blake3, chosen);
    }

    [Fact]
    public void Choose_UserChoiceRestoredAfterPassingThroughOtherLengths()
    {
        // Typing a hash passes through 32 and 40 chars: the selection follows, then returns to the user's pick at 64
        var current = HashAlgorithmId.Blake3;
        HashAlgorithmId? user = HashAlgorithmId.Blake3;

        current = AlgorithmSelector.Choose(Hash(32, HashAlgorithmId.Md5), current, user) ?? current;
        Assert.Equal(HashAlgorithmId.Md5, current);

        current = AlgorithmSelector.Choose(Hash(40, HashAlgorithmId.Sha1), current, user) ?? current;
        Assert.Equal(HashAlgorithmId.Sha1, current);

        current = AlgorithmSelector.Choose(Hash(64, HashAlgorithmId.Sha256), current, user) ?? current;
        Assert.Equal(HashAlgorithmId.Blake3, current);
    }

    [Fact]
    public void Choose_UserChoiceDoesNotFit_FallsBackToSuggestion()
    {
        // User picked BLAKE3 (64) but pasted a 128-char hash
        var chosen = AlgorithmSelector.Choose(Hash(128, HashAlgorithmId.Sha512), HashAlgorithmId.Blake3, HashAlgorithmId.Blake3);
        Assert.Equal(HashAlgorithmId.Sha512, chosen);
    }

    [Fact]
    public void Choose_NoUserChoice_CurrentAlreadyFits_NoChange()
    {
        // SHA3-256 was selected by an earlier auto-detection; 64 chars still fits
        Assert.Null(AlgorithmSelector.Choose(Hash(64, HashAlgorithmId.Sha256), HashAlgorithmId.Sha3_256, null));
    }

    [Fact]
    public void Choose_NoUserChoice_CurrentDoesNotFit_UsesSuggestion()
    {
        Assert.Equal(HashAlgorithmId.Sha256,
            AlgorithmSelector.Choose(Hash(64, HashAlgorithmId.Sha256), HashAlgorithmId.Md5, null));
    }

    // ── Ambiguity note ───────────────────────────────────────────────────────

    [Fact]
    public void DescribeAmbiguity_64Chars_ListsAllSupportedCandidates()
    {
        var note = AlgorithmSelector.DescribeAmbiguity(Hash(64, HashAlgorithmId.Sha256), HashAlgorithmId.Sha256, _ => true);
        Assert.NotNull(note);
        Assert.Contains("64-character", note);
        foreach (var name in new[] { "SHA-256", "SHA3-256", "BLAKE3", "BLAKE2b-256" })
            Assert.Contains(name, note);
        Assert.Contains("Selected: SHA-256", note);
    }

    [Fact]
    public void DescribeAmbiguity_UnsupportedAlgorithmsAreNotListed()
    {
        var note = AlgorithmSelector.DescribeAmbiguity(
            Hash(64, HashAlgorithmId.Sha256), HashAlgorithmId.Sha256, id => id != HashAlgorithmId.Sha3_256);
        Assert.DoesNotContain("SHA3-256", note);
        Assert.Contains("BLAKE3", note);
    }

    [Fact]
    public void DescribeAmbiguity_128Chars_ListsSha512Variants()
    {
        var note = AlgorithmSelector.DescribeAmbiguity(Hash(128, HashAlgorithmId.Sha512), HashAlgorithmId.Sha512, _ => true);
        Assert.NotNull(note);
        Assert.Contains("SHA-512", note);
        Assert.Contains("SHA3-512", note);
        Assert.Contains("BLAKE2b-512", note);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(40)]
    public void DescribeAmbiguity_UniqueLength_ReturnsNull(int length)
    {
        Assert.Null(AlgorithmSelector.DescribeAmbiguity(Hash(length, null), HashAlgorithmId.Md5, _ => true));
    }

    [Fact]
    public void DescribeAmbiguity_ExplicitPrefix_ReturnsNull()
    {
        var detected = Hash(64, HashAlgorithmId.Blake3, explicitPrefix: true);
        Assert.Null(AlgorithmSelector.DescribeAmbiguity(detected, HashAlgorithmId.Blake3, _ => true));
    }

    [Fact]
    public void DescribeAmbiguity_NoHash_ReturnsNull()
    {
        Assert.Null(AlgorithmSelector.DescribeAmbiguity(null, HashAlgorithmId.Sha256, _ => true));
    }

    [Theory]
    [InlineData(HashAlgorithmId.Sha256, "SHA-256")]
    [InlineData(HashAlgorithmId.Sha3_512, "SHA3-512")]
    [InlineData(HashAlgorithmId.Blake2b_256, "BLAKE2b-256")]
    [InlineData(HashAlgorithmId.Sha1, "SHA-1")]
    public void DisplayName_MatchesUiLabels(HashAlgorithmId id, string expected)
    {
        Assert.Equal(expected, id.DisplayName());
    }

    [Fact]
    public void DisplayName_CoversEveryAlgorithm()
    {
        foreach (var id in Enum.GetValues<HashAlgorithmId>())
            Assert.False(string.IsNullOrWhiteSpace(id.DisplayName()));
    }
}
