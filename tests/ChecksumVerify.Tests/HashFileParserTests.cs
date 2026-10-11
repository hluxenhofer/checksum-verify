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
    [InlineData(".b2",       HashAlgorithmId.Blake2b_256)]
    [InlineData(".blake2",   HashAlgorithmId.Blake2b_256)]
    [InlineData(".blake2b",  HashAlgorithmId.Blake2b_256)]
    [InlineData(".b3",       HashAlgorithmId.Blake3)]
    [InlineData(".blake3",   HashAlgorithmId.Blake3)]
    [InlineData(".md5",      HashAlgorithmId.Md5)]
    public void AlgorithmFromExtension_KnownExtension(string extension, HashAlgorithmId expected)
    {
        Assert.Equal(expected, HashFileParser.AlgorithmFromExtension(extension));
    }

    [Fact]
    public void AlgorithmFromExtension_Unknown_ReturnsNull()
    {
        Assert.Null(HashFileParser.AlgorithmFromExtension(".txt"));
    }
}
