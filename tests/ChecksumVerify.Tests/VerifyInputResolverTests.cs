using ChecksumVerify.Core;
using Xunit;

namespace ChecksumVerify.Tests;

public class VerifyInputResolverTests
{
    private const string HelloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    private static string WriteTemp(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, content);
        return path;
    }

    private static VerifyInput FromFile(string extension, string content, string target, HashAlgorithmId selected,
        Func<HashAlgorithmId, bool>? isSupported = null)
    {
        var path = WriteTemp(extension, content);
        try { return VerifyInputResolver.FromHashFile(path, target, selected, isSupported ?? (_ => true)); }
        finally { File.Delete(path); }
    }

    // ── Typed / pasted hash ──────────────────────────────────────────────────

    [Fact]
    public void FromHash_Valid_ReturnsExpectedAndSelectedAlgorithm()
    {
        var input = VerifyInputResolver.FromHash(HelloSha256, HashAlgorithmId.Sha256);
        Assert.Null(input.Error);
        Assert.Equal(HelloSha256, input.Expected);
        Assert.Equal(HashAlgorithmId.Sha256, input.Algorithm);
    }

    [Fact]
    public void FromHash_StripsAlgorithmPrefix()
    {
        var input = VerifyInputResolver.FromHash("sha256:" + HelloSha256, HashAlgorithmId.Sha256);
        Assert.Equal(HelloSha256, input.Expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromHash_Empty_ReportsNoHash(string? text)
    {
        var input = VerifyInputResolver.FromHash(text, HashAlgorithmId.Sha256);
        Assert.Null(input.Expected);
        Assert.Contains("No hash provided", input.Error);
    }

    [Fact]
    public void FromHash_NonHex_ReportsInvalidCharacters()
    {
        var input = VerifyInputResolver.FromHash(new string('z', 64), HashAlgorithmId.Sha256);
        Assert.Null(input.Expected);
        Assert.Contains("hexadecimal", input.Error);
    }

    [Fact]
    public void FromHash_WrongLengthForAlgorithm_NamesBothLengths()
    {
        var input = VerifyInputResolver.FromHash(new string('a', 40), HashAlgorithmId.Sha256);
        Assert.Null(input.Expected);
        Assert.Contains("40 characters", input.Error);
        Assert.Contains("SHA-256", input.Error);
        Assert.Contains("64", input.Error);
    }

    // ── Hash file ────────────────────────────────────────────────────────────

    [Fact]
    public void FromHashFile_NoFileSelected_ReportsIt()
    {
        var input = VerifyInputResolver.FromHashFile(null, "hello.bin", HashAlgorithmId.Sha256);
        Assert.Contains("No hash file selected", input.Error);
    }

    [Fact]
    public void FromHashFile_NoEntryForTarget_ReportsFileName()
    {
        var input = FromFile(".sha256sum", $"{HelloSha256}  other.bin\n", "hello.bin", HashAlgorithmId.Sha256);
        Assert.Null(input.Expected);
        Assert.Contains("No entry found for hello.bin", input.Error);
    }

    [Fact]
    public void FromHashFile_CertainAlgorithm_OverridesUserSelectionOfSameLength()
    {
        // .sha256sum says SHA-256 even though the dropdown shows BLAKE3 (same length)
        var input = FromFile(".sha256sum", $"{HelloSha256}  hello.bin\n", "hello.bin", HashAlgorithmId.Blake3);
        Assert.Null(input.Error);
        Assert.Equal(HashAlgorithmId.Sha256, input.Algorithm);
    }

    [Fact]
    public void FromHashFile_GuessedAlgorithm_KeepsUserSelectionWhenLengthFits()
    {
        // No tag, no known extension: 64 chars is only "probably" SHA-256, so the user's BLAKE3 stays
        var input = FromFile(".txt", $"{HelloSha256}  hello.bin\n", "hello.bin", HashAlgorithmId.Blake3);
        Assert.Null(input.Error);
        Assert.Equal(HashAlgorithmId.Blake3, input.Algorithm);
    }

    [Fact]
    public void FromHashFile_GuessedAlgorithm_UsedWhenSelectionCannotFit()
    {
        var input = FromFile(".txt", $"{HelloSha256}  hello.bin\n", "hello.bin", HashAlgorithmId.Md5);
        Assert.Null(input.Error);
        Assert.Equal(HashAlgorithmId.Sha256, input.Algorithm);
    }

    [Fact]
    public void FromHashFile_BsdTag_OverridesSelection()
    {
        var input = FromFile(".txt", $"SHA512 (hello.bin) = {new string('b', 128)}\n", "hello.bin", HashAlgorithmId.Sha256);
        Assert.Null(input.Error);
        Assert.Equal(HashAlgorithmId.Sha512, input.Algorithm);
    }

    [Fact]
    public void FromHashFile_UnsupportedAlgorithm_ReportsRequirement()
    {
        var input = FromFile(".sha3-256", new string('c', 64), "x.bin", HashAlgorithmId.Sha256,
            isSupported: id => id != HashAlgorithmId.Sha3_256);
        Assert.Null(input.Expected);
        Assert.Equal(HashAlgorithmId.Sha3_256, input.Algorithm);
        Assert.Contains("SHA3-256", input.Error);
        Assert.Contains(HashAlgorithmNames.Sha3Requirement, input.Error);
    }

    [Fact]
    public void FromHashFile_B2Sidecar_PicksVariantByLength()
    {
        Assert.Equal(HashAlgorithmId.Blake2b_512,
            FromFile(".b2", new string('a', 128), "x.bin", HashAlgorithmId.Sha256).Algorithm);
        Assert.Equal(HashAlgorithmId.Blake2b_256,
            FromFile(".b2", new string('a', 64), "x.bin", HashAlgorithmId.Sha256).Algorithm);
    }

    [Fact]
    public void FromHashFile_MissingFile_Throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sha256");
        Assert.ThrowsAny<IOException>(() => VerifyInputResolver.FromHashFile(missing, "x.bin", HashAlgorithmId.Sha256));
    }
}
