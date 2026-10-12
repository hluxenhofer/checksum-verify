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

    [Fact]
    public async Task Sha3_256_HelloFile_ReturnsKnownHash()
    {
        Assert.SkipUnless(HashEngine.IsSupported(HashAlgorithmId.Sha3_256), "SHA3 requires Windows 11 Build 25324+ (or OpenSSL 1.1.1+)");
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha3_256, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("3338be694f50c5f338814986cdf0686453a888b84f424d792af4b9202398f392", result);
    }

    [Fact]
    public async Task Sha3_512_HelloFile_ReturnsKnownHash()
    {
        Assert.SkipUnless(HashEngine.IsSupported(HashAlgorithmId.Sha3_512), "SHA3 requires Windows 11 Build 25324+ (or OpenSSL 1.1.1+)");
        var result = await HashEngine.ComputeAsync(FixturePath("hello.bin"), HashAlgorithmId.Sha3_512, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("75d527c368f2efe848ecf6b073a36767800805e9eef2b1857d5f984f036eb6df891d75f72d9b154518c1cd58835286d1da9a38deba3de98b5a53e5ed78a84976", result);
    }

    [Fact]
    public async Task ComputeAsync_ProgressIsThrottledAndEndsAtFileLength()
    {
        const int length = 3 * 1024 * 1024 + 123;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        await File.WriteAllBytesAsync(path, new byte[length], TestContext.Current.CancellationToken);
        try
        {
            var reports = new List<long>();
            await HashEngine.ComputeAsync(path, HashAlgorithmId.Sha256, new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);
            Assert.Equal(length, reports[^1]);
            // 50 chunks of 64 KB, reported every 16th chunk plus once at the end
            Assert.InRange(reports.Count, 2, 6);
        }
        finally { File.Delete(path); }
    }

    private sealed class SynchronousProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    [Theory]
    [InlineData(HashAlgorithmId.Md5, 32)]
    [InlineData(HashAlgorithmId.Sha1, 40)]
    [InlineData(HashAlgorithmId.Sha256, 64)]
    [InlineData(HashAlgorithmId.Sha3_256, 64)]
    [InlineData(HashAlgorithmId.Blake3, 64)]
    [InlineData(HashAlgorithmId.Blake2b_256, 64)]
    [InlineData(HashAlgorithmId.Sha512, 128)]
    [InlineData(HashAlgorithmId.Sha3_512, 128)]
    [InlineData(HashAlgorithmId.Blake2b_512, 128)]
    public void HexLength_MatchesDigestSize(HashAlgorithmId algorithm, int expected)
    {
        Assert.Equal(expected, HashEngine.HexLength(algorithm));
    }

    [Fact]
    public void CandidatesForHexLength_64_ContainsSupported256BitAlgorithms()
    {
        var candidates = HashEngine.CandidatesForHexLength(64);
        Assert.Contains(HashAlgorithmId.Sha256, candidates);
        Assert.Contains(HashAlgorithmId.Blake3, candidates);
        Assert.Contains(HashAlgorithmId.Blake2b_256, candidates);
        Assert.DoesNotContain(HashAlgorithmId.Sha512, candidates);
    }

    [Fact]
    public void CandidatesForHexLength_32_IsOnlyMd5()
    {
        Assert.Equal([HashAlgorithmId.Md5], HashEngine.CandidatesForHexLength(32));
    }
}
