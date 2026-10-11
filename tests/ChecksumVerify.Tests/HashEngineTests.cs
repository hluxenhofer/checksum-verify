using ChecksumVerify.Core;
using Xunit;

namespace ChecksumVerify.Tests;

public class HashEngineTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task Sha256_EmptyFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("empty.bin"), HashAlgorithmId.Sha256, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result);
    }

    [Fact]
    public async Task Sha256_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha256, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
    }

    [Fact]
    public async Task Sha512_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha512, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("9b71d224bd62f3785d96d46ad3ea3d73319bfbc2890caadae2dff72519673ca72323c3d99ba5c11d7c7acc6e14b8c5da0c4663475c2e5c3adef46f73bcdec043", result);
    }

    [Fact]
    public async Task Blake3_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Blake3, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ea8f163db38682925e4491c5e58d4bb3506ef8c14eb78a86e908c5624a67200f", result);
    }

    [Fact]
    public async Task Blake2b256_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Blake2b_256, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("324dcf027dd4a30a932c441f365a25e86b173defa4b8e58948253471b81b72cf", result);
    }

    [Fact]
    public async Task Md5_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Md5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", result);
    }

    [Fact]
    public async Task Sha1_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", result);
    }

    [Fact]
    public async Task Blake2b512_HelloFile_ReturnsKnownHash()
    {
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Blake2b_512, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("e4cfa39a3d37be31c59609e807970799caa68a19bfaa15135f165085e01d41a65ba1e1b146aeb6bd0092b49eac214c103ccfa3a365954bbbe52f74a2b3620c94", result);
    }

    [Fact]
    public async Task Verify_MatchesIgnoresCase()
    {
        Assert.True(HashEngine.Verify("ABCDEF", "abcdef"));
    }

    [Fact]
    public async Task Verify_MatchesWithLeadingWhitespace()
    {
        Assert.True(HashEngine.Verify("  abcdef  ", "abcdef"));
    }

    [Fact]
    public async Task Verify_ReturnsFalseOnMismatch()
    {
        Assert.False(HashEngine.Verify("aaa", "bbb"));
    }

    [Fact]
    public async Task ComputeAsync_ReportsProgress()
    {
        long? lastReport = null;
        var progress = new Progress<long>(p => lastReport = p);
        await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha256, progress, cancellationToken: TestContext.Current.CancellationToken);
        await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(lastReport);
    }

    [Fact]
    public async Task ComputeAsync_CancellationCancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha256, cancellationToken: cts.Token));
    }

    [Theory]
    [InlineData(HashAlgorithmId.Sha256)]
    [InlineData(HashAlgorithmId.Sha512)]
    [InlineData(HashAlgorithmId.Blake3)]
    [InlineData(HashAlgorithmId.Blake2b_256)]
    [InlineData(HashAlgorithmId.Blake2b_512)]
    [InlineData(HashAlgorithmId.Md5)]
    [InlineData(HashAlgorithmId.Sha1)]
    public void IsSupported_ReturnsTrueForNonSha3(HashAlgorithmId algorithm)
    {
        Assert.True(HashEngine.IsSupported(algorithm));
    }
}
