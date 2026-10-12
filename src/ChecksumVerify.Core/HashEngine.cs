using System.Security.Cryptography;
using Blake2Fast;
using Blake3;

namespace ChecksumVerify.Core;

public static class HashEngine
{
    private const int BufferSize = 65_536;

    public static bool IsSupported(HashAlgorithmId algorithm) => algorithm switch
    {
        HashAlgorithmId.Sha3_256 => SHA3_256.IsSupported,
        HashAlgorithmId.Sha3_512 => SHA3_512.IsSupported,
        _ => true
    };

    /// <summary>Length of the hex digest in characters.</summary>
    public static int HexLength(HashAlgorithmId algorithm) => algorithm switch
    {
        HashAlgorithmId.Md5 => 32,
        HashAlgorithmId.Sha1 => 40,
        HashAlgorithmId.Sha256 or HashAlgorithmId.Sha3_256 or HashAlgorithmId.Blake3 or HashAlgorithmId.Blake2b_256 => 64,
        HashAlgorithmId.Sha512 or HashAlgorithmId.Sha3_512 or HashAlgorithmId.Blake2b_512 => 128,
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
    };

    /// <summary>Supported algorithms that produce a digest of the given hex length.</summary>
    public static IReadOnlyList<HashAlgorithmId> CandidatesForHexLength(int hexLength) =>
        Enum.GetValues<HashAlgorithmId>()
            .Where(a => HexLength(a) == hexLength && IsSupported(a))
            .ToArray();

    public static async Task<string> ComputeAsync(
        string filePath,
        HashAlgorithmId algorithm,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // bufferSize 0: we read in our own 64 KB chunks, so FileStream's buffer would only add a copy
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 0,
            useAsync: true);

        return algorithm switch
        {
            HashAlgorithmId.Sha256      => await ComputeIncrementalAsync(SHA256.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Sha512      => await ComputeIncrementalAsync(SHA512.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Sha3_256    => await ComputeIncrementalAsync(SHA3_256.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Sha3_512    => await ComputeIncrementalAsync(SHA3_512.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Md5         => await ComputeIncrementalAsync(MD5.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Sha1        => await ComputeIncrementalAsync(SHA1.Create(), stream, progress, cancellationToken),
            HashAlgorithmId.Blake3      => await ComputeBlake3Async(stream, progress, cancellationToken),
            HashAlgorithmId.Blake2b_256 => await ComputeBlake2bAsync(stream, 32, progress, cancellationToken),
            HashAlgorithmId.Blake2b_512 => await ComputeBlake2bAsync(stream, 64, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
    }

    public static bool Verify(string computed, string expected) =>
        string.Equals(computed.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    // Reports progress about once per MiB (and once at the end) instead of once per chunk:
    // a 10 GB file would otherwise post ~160,000 UI updates.
    private const int ChunksPerProgressReport = 16;

    private static async Task ReadChunksAsync(
        Stream stream,
        Action<byte[], int> consume,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        int chunks = 0;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            consume(buffer, bytesRead);
            if (++chunks % ChunksPerProgressReport == 0)
                progress?.Report(stream.Position);
        }
        progress?.Report(stream.Position);
    }

    private static async Task<string> ComputeIncrementalAsync(
        HashAlgorithm hasher,
        Stream stream,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using (hasher)
        {
            await ReadChunksAsync(stream, (buffer, count) => hasher.TransformBlock(buffer, 0, count, null, 0), progress, cancellationToken);
            hasher.TransformFinalBlock([], 0, 0);
            return Convert.ToHexString(hasher.Hash!).ToLowerInvariant();
        }
    }

    private static async Task<string> ComputeBlake3Async(
        Stream stream,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using var hasher = Hasher.New();
        await ReadChunksAsync(stream, (buffer, count) => hasher.Update(buffer.AsSpan(0, count)), progress, cancellationToken);
        return hasher.Finalize().ToString();
    }

    private static async Task<string> ComputeBlake2bAsync(
        Stream stream,
        int outputBytes,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var hasher = Blake2b.CreateIncrementalHasher(outputBytes);
        await ReadChunksAsync(stream, (buffer, count) => hasher.Update(buffer.AsSpan(0, count)), progress, cancellationToken);
        byte[] result = new byte[outputBytes];
        hasher.Finish(result.AsSpan());
        return Convert.ToHexString(result).ToLowerInvariant();
    }
}
