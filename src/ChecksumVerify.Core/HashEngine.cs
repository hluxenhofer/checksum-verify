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

    public static async Task<string> ComputeAsync(
        string filePath,
        HashAlgorithmId algorithm,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: BufferSize,
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

    private static async Task<string> ComputeIncrementalAsync(
        HashAlgorithm hasher,
        Stream stream,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using (hasher)
        {
            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                progress?.Report(stream.Position);
            }
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
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, bytesRead));
            progress?.Report(stream.Position);
        }
        return hasher.Finalize().ToString();
    }

    private static async Task<string> ComputeBlake2bAsync(
        Stream stream,
        int outputBytes,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var hasher = Blake2b.CreateIncrementalHasher(outputBytes);
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, bytesRead));
            progress?.Report(stream.Position);
        }
        byte[] result = new byte[outputBytes];
        hasher.Finish(result.AsSpan());
        return Convert.ToHexString(result).ToLowerInvariant();
    }
}
