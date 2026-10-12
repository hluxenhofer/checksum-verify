using Xunit;
using ChecksumVerify.Core;

namespace ChecksumVerify.Tests;

public class HashFileParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void TryParse_GnuFormat_FindsTargetFile()
    {
        var result = HashFileParser.TryParse(FixturePath("checksums.sha256sum"), "hello.bin");
        Assert.NotNull(result);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result!.Hash);
        Assert.Equal(HashAlgorithmId.Sha256, result.Algorithm);
    }

    [Fact]
    public void TryParse_GnuFormat_SecondEntry()
    {
        var result = HashFileParser.TryParse(FixturePath("checksums.sha256sum"), "empty.bin");
        Assert.NotNull(result);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result!.Hash);
    }

    [Fact]
    public void TryParse_GnuFormat_UnknownFilename_ReturnsNull()
    {
        var result = HashFileParser.TryParse(FixturePath("checksums.sha256sum"), "other.bin");
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_GnuFormat_BinaryMode_StarPrefix()
    {
        var tmp = Path.GetTempFileName() + ".sha256sum";
        File.WriteAllText(tmp, "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824 *hello.bin\n");
        try
        {
            var result = HashFileParser.TryParse(tmp, "hello.bin");
            Assert.NotNull(result);
            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result!.Hash);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void TryParse_Sha256Sidecar_ReturnsBareHash()
    {
        var result = HashFileParser.TryParse(FixturePath("hello.bin.sha256"), "hello.bin");
        Assert.NotNull(result);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result!.Hash);
        Assert.Equal(HashAlgorithmId.Sha256, result.Algorithm);
    }

    [Theory]
    [InlineData(".sha256",   HashAlgorithmId.Sha256)]
    [InlineData(".sha512",   HashAlgorithmId.Sha512)]
    [InlineData(".sha3-256", HashAlgorithmId.Sha3_256)]
    [InlineData(".sha3-512", HashAlgorithmId.Sha3_512)]
    [InlineData(".b2",       HashAlgorithmId.Blake2b_512)]
    [InlineData(".blake2",   HashAlgorithmId.Blake2b_512)]
    [InlineData(".blake2b",  HashAlgorithmId.Blake2b_512)]
    [InlineData(".b3",       HashAlgorithmId.Blake3)]
    [InlineData(".blake3",   HashAlgorithmId.Blake3)]
    [InlineData(".md5",      HashAlgorithmId.Md5)]
    [InlineData(".sha1",     HashAlgorithmId.Sha1)]
    public void AlgorithmFromExtension_KnownExtension(string extension, HashAlgorithmId expected)
    {
        Assert.Equal(expected, HashFileParser.AlgorithmFromExtension(extension));
    }

    [Fact]
    public void AlgorithmFromExtension_Unknown_ReturnsNull()
    {
        Assert.Null(HashFileParser.AlgorithmFromExtension(".txt"));
    }

    private const string HelloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    private static ParsedHash? ParseContent(string extension, string content, string target)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, content);
        try { return HashFileParser.TryParse(path, target); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(".sha256sum", HashAlgorithmId.Sha256)]
    [InlineData(".sha512sum", HashAlgorithmId.Sha512)]
    [InlineData(".SHA256", HashAlgorithmId.Sha256)]
    public void AlgorithmForHashFile_IncludesGnuListExtensions(string extension, HashAlgorithmId expected)
    {
        Assert.Equal(expected, HashFileParser.AlgorithmForHashFile("file" + extension));
    }

    [Fact]
    public void TryParse_Sidecar_GnuLineForOtherFile_ReturnsNull()
    {
        Assert.Null(ParseContent(".sha256", $"{HelloSha256}  other.bin\n", "hello.bin"));
    }

    [Fact]
    public void TryParse_Sidecar_GnuLineForTargetFile_ReturnsHash()
    {
        var result = ParseContent(".sha256", $"{HelloSha256}  hello.bin\n", "hello.bin");
        Assert.Equal(HelloSha256, result?.Hash);
    }

    [Fact]
    public void TryParse_Sidecar_SkipsCommentsAndBlankLines()
    {
        var result = ParseContent(".sha256", $"# comment\n\n{HelloSha256}\n", "hello.bin");
        Assert.Equal(HelloSha256, result?.Hash);
    }

    [Fact]
    public void TryParse_B2Sidecar_512BitHash_SelectsBlake2b512()
    {
        var result = ParseContent(".b2", new string('a', 128), "x.bin");
        Assert.Equal(HashAlgorithmId.Blake2b_512, result?.Algorithm);
    }

    [Fact]
    public void TryParse_B2Sidecar_256BitHash_SelectsBlake2b256()
    {
        var result = ParseContent(".b2", new string('a', 64), "x.bin");
        Assert.Equal(HashAlgorithmId.Blake2b_256, result?.Algorithm);
    }

    [Fact]
    public void TryParse_UnknownExtension_InfersAlgorithmFromHashLength()
    {
        var result = ParseContent(".txt", $"{HelloSha256}  hello.bin\n", "hello.bin");
        Assert.Equal(HashAlgorithmId.Sha256, result?.Algorithm);
    }

    [Fact]
    public void TryParse_BsdTagFormat_UsesTagAlgorithm()
    {
        var result = ParseContent(".txt", $"SHA512 (hello.bin) = {new string('b', 128)}\n", "hello.bin");
        Assert.NotNull(result);
        Assert.Equal(new string('b', 128), result!.Hash);
        Assert.Equal(HashAlgorithmId.Sha512, result.Algorithm);
    }

    [Fact]
    public void TryParse_BsdTagFormat_FileNameWithSpacesAndParentheses()
    {
        var result = ParseContent(".txt", $"SHA256 (my file (1).bin) = {HelloSha256}\n", "my file (1).bin");
        Assert.Equal(HelloSha256, result?.Hash);
    }

    [Fact]
    public void TryParse_GnuFormat_FileNameWithSpaces()
    {
        var result = ParseContent(".sha256sum", $"{HelloSha256}  my file.bin\n", "my file.bin");
        Assert.Equal(HelloSha256, result?.Hash);
    }

    [Fact]
    public void TryParse_GnuFormat_EscapedBackslashInFileName()
    {
        // sha256sum prints "\<hash>  a\\b.bin" for a file named a\b.bin
        var result = ParseContent(".sha256sum", "\\" + HelloSha256 + "  a\\\\b.bin\n", "a\\b.bin");
        Assert.Equal(HelloSha256, result?.Hash);
    }

    [Fact]
    public void TryParse_Sha3Sidecar_ReportsSha3Algorithm()
    {
        var result = ParseContent(".sha3-256", new string('c', 64), "x.bin");
        Assert.Equal(HashAlgorithmId.Sha3_256, result?.Algorithm);
    }
}
