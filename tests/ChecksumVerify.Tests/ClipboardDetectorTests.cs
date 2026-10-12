using Xunit;
using ChecksumVerify.Core;

namespace ChecksumVerify.Tests;

public class ClipboardDetectorTests
{
    [Theory]
    [InlineData("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", true)]
    [InlineData("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", true)]
    [InlineData("5d41402abc4b2a76b9719d911017c592", true)]
    [InlineData("not a hash", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824 extra", false)]
    public void IsHexString_VariousInputs(string? input, bool expected)
    {
        Assert.Equal(expected, ClipboardDetector.IsHexString(input));
    }

    [Theory]
    [InlineData(32,  HashAlgorithmId.Md5)]
    [InlineData(40,  HashAlgorithmId.Sha1)]
    [InlineData(64,  HashAlgorithmId.Sha256)]
    [InlineData(128, HashAlgorithmId.Sha512)]
    public void SuggestAlgorithm_KnownLength(int length, HashAlgorithmId expected)
    {
        var hex = new string('a', length);
        Assert.Equal(expected, ClipboardDetector.SuggestAlgorithm(hex));
    }

    [Fact]
    public void SuggestAlgorithm_96Chars_ReturnsNull()
    {
        Assert.Null(ClipboardDetector.SuggestAlgorithm(new string('a', 96)));
    }

    [Fact]
    public void SuggestAlgorithm_UnknownLength_ReturnsNull()
    {
        Assert.Null(ClipboardDetector.SuggestAlgorithm(new string('a', 10)));
    }

    [Fact]
    public void TryDetect_ValidHex_ReturnsDetectedHash()
    {
        var hex = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";
        var result = ClipboardDetector.TryDetect(hex);
        Assert.NotNull(result);
        Assert.Equal(hex, result!.Hash);
        Assert.Equal(HashAlgorithmId.Sha256, result.SuggestedAlgorithm);
    }

    [Fact]
    public void TryDetect_ValidHexWithWhitespace_StripsAndReturns()
    {
        var hex = "  2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824  ";
        var result = ClipboardDetector.TryDetect(hex);
        Assert.NotNull(result);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result!.Hash);
    }

    [Fact]
    public void TryDetect_UppercaseHex_NormalizesToLowercase()
    {
        var result = ClipboardDetector.TryDetect("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824");
        Assert.NotNull(result);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result!.Hash);
    }

    [Fact]
    public void TryDetect_NonHex_ReturnsNull()
    {
        Assert.Null(ClipboardDetector.TryDetect("hello world"));
    }

    [Fact]
    public void TryDetect_Null_ReturnsNull()
    {
        Assert.Null(ClipboardDetector.TryDetect(null));
    }

    [Theory]
    [InlineData("sha256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", HashAlgorithmId.Sha256)]
    [InlineData("SHA256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", HashAlgorithmId.Sha256)]
    [InlineData("sha-256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", HashAlgorithmId.Sha256)]
    [InlineData("sha512:cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e", "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e", HashAlgorithmId.Sha512)]
    [InlineData("md5:5d41402abc4b2a76b9719d911017c592", "5d41402abc4b2a76b9719d911017c592", HashAlgorithmId.Md5)]
    [InlineData("blake3:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", HashAlgorithmId.Blake3)]
    public void TryDetect_AlgoPrefixedHash_StripsAndSelectsAlgorithm(string input, string expectedHash, HashAlgorithmId expectedAlgo)
    {
        var result = ClipboardDetector.TryDetect(input);
        Assert.NotNull(result);
        Assert.Equal(expectedHash, result!.Hash);
        Assert.Equal(expectedAlgo, result.SuggestedAlgorithm);
    }

    [Theory]
    [InlineData("sha256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    [InlineData("SHA256:2CF24DBA", "2CF24DBA")]
    [InlineData("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    [InlineData("  sha256:abc123  ", "abc123")]
    public void StripAlgoPrefix_VariousInputs_ReturnsHexOnly(string input, string expected)
    {
        Assert.Equal(expected, ClipboardDetector.StripAlgoPrefix(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2026")]
    [InlineData("deadbeef")]
    public void TryDetect_ImplausibleHexLength_ReturnsNull(string input)
    {
        Assert.Null(ClipboardDetector.TryDetect(input));
    }

    [Fact]
    public void TryDetect_UnprefixedHash_IsNotExplicit()
    {
        Assert.False(ClipboardDetector.TryDetect(new string('a', 64))!.IsExplicit);
    }

    [Fact]
    public void TryDetect_PrefixedHash_IsExplicit()
    {
        Assert.True(ClipboardDetector.TryDetect("blake3:" + new string('a', 64))!.IsExplicit);
    }

    [Theory]
    [InlineData(64, HashAlgorithmId.Blake2b_256)]
    [InlineData(128, HashAlgorithmId.Blake2b_512)]
    public void TryDetect_Blake2bPrefix_PicksVariantByLength(int length, HashAlgorithmId expected)
    {
        var result = ClipboardDetector.TryDetect("blake2b:" + new string('a', length));
        Assert.Equal(expected, result?.SuggestedAlgorithm);
    }
}
