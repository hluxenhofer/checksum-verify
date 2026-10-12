namespace ChecksumVerify.Core;

public static class HashAlgorithmNames
{
    /// <summary>OS requirement for SHA-3, shown in messages and the algorithm list.</summary>
    public const string Sha3Requirement = "Windows 11 24H2+";

    public static string DisplayName(this HashAlgorithmId id) => id switch
    {
        HashAlgorithmId.Sha256      => "SHA-256",
        HashAlgorithmId.Sha512      => "SHA-512",
        HashAlgorithmId.Sha3_256    => "SHA3-256",
        HashAlgorithmId.Sha3_512    => "SHA3-512",
        HashAlgorithmId.Blake3      => "BLAKE3",
        HashAlgorithmId.Blake2b_256 => "BLAKE2b-256",
        HashAlgorithmId.Blake2b_512 => "BLAKE2b-512",
        HashAlgorithmId.Md5         => "MD5",
        HashAlgorithmId.Sha1        => "SHA-1",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
