using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Descriptors;

public record Pbes2KeyWrapAlgorithmDescriptor
(
    string AlgorithmCode,
    HashAlgorithmName HashAlgorithmName,
    int HashSizeBits,
    int KeySizeBits,
    int DefaultIterationCount
) : KeyWrapAlgorithmDescriptor
(
    Pbes2CryptoFactory.Default,
    typeof(SharedSecretKey),
    AlgorithmCode
)
{
    /// <summary>
    /// Gets the number of bytes for hash of the key agreement.
    /// </summary>
    public int HashSizeBytes => HashSizeBits / BinaryUtility.BitsPerByte;

    /// <summary>
    /// Gets the number of bytes for the <c>key encryption key (kek)</c>.
    /// </summary>
    public int KeySizeBytes => KeySizeBits / BinaryUtility.BitsPerByte;
}
