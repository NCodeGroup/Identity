// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Json;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES</c> cryptographic
/// algorithm for key management. It accepts every key type that has a registered <see cref="IEcdhKeyAgreement"/>, and
/// writes and reads the ephemeral public key (<c>epk</c>) as a JSON Web Key through that key type's
/// <see cref="ISecretKeyTypeDescriptor"/>, so neither the <c>kty</c> nor the members of the <c>epk</c> are known to
/// the algorithm.
/// </summary>
// See ADR-1389.
// See ADR-1477.
internal class EcdhKeyManagementAlgorithm : CommonKeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticCekByteSizes { get; } =
    [new(minSize: 1, maxSize: int.MaxValue, skipSize: 1)];

    /// <summary>
    /// Merges the key types of agreements that serve the same runtime key type, such as two agreements over different
    /// <c>EC</c> curves, into one <see cref="AlgorithmKeyType"/> with the curves of both, so a package can add an
    /// agreement over another curve of a key type the core already serves. The key types keep the order the agreements
    /// were registered in: the core's first, through <c>AddJoseLibrary()</c>, then each package's in the order the host
    /// adds them. The agreements of one key type either all name their curves or all name sizes only: an agreement
    /// without curves accepts every curve of its sizes, so beside one on curves the merged key type could list no curve,
    /// and the composition fails.
    /// </summary>
    /// <exception cref="InvalidOperationException">An agreement that names curves and one that does not serve the same
    /// key type.</exception>
    private static AlgorithmKeyType[] MergeKeyTypes(IEnumerable<IEcdhKeyAgreement> keyAgreements) =>
        [
            .. keyAgreements
                .GroupBy(keyAgreement => keyAgreement.AlgorithmKeyType.KeyClrType)
                .Select(group =>
                {
                    var keyTypes = group
                        .Select(keyAgreement => keyAgreement.AlgorithmKeyType)
                        .ToList();
                    if (keyTypes.Count == 1)
                    {
                        return keyTypes[0];
                    }

                    var onCurves = keyTypes.Count(keyType => keyType.Curves.Count > 0);
                    if (onCurves == 0)
                    {
                        return new AlgorithmKeyType(
                            group.Key,
                            keyTypes.SelectMany(keyType => keyType.KeyBitSizes)
                        );
                    }

                    if (onCurves != keyTypes.Count)
                    {
                        throw new InvalidOperationException(
                            $"The ECDH key agreements for the key type '{group.Key.FullName}' mix agreements that "
                                + "name their curves with agreements that do not; every agreement for one key type "
                                + "names its curves, or none does."
                        );
                    }

                    return AlgorithmKeyType.OnCurves(
                        group.Key,
                        keyTypes
                            .SelectMany(keyType => keyType.Curves)
                            .DistinctBy(curve => curve.Name, StringComparer.Ordinal)
                    );
                }),
        ];

    /// <summary>
    /// Writes the ephemeral public key as the <c>epk</c> JSON Web Key: the descriptor exports the key and only the
    /// required members of the key type (RFC 7638 §3.2) are kept, so the <c>epk</c> carries the minimum that
    /// represents the key (RFC 7518 §4.6.1.1) and never a private member.
    /// </summary>
    private static Dictionary<string, object> WriteEphemeralKey(
        ISecretKeyTypeDescriptor descriptor,
        JsonWebKeyTypeDescriptor jwkKeyType,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    )
    {
        SecretKey ephemeralPublicKey;
        try
        {
            ephemeralPublicKey = descriptor.DeserializeKeyMaterial(
                default,
                SecretEncodings.Spki,
                subjectPublicKeyInfo
            );
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or CryptographicException)
        {
            throw new JoseException(
                "The ephemeral public key cannot be read by its key type.",
                exception
            );
        }

        var exported =
            descriptor.ExportPublicKey(ephemeralPublicKey)
            ?? throw new JoseException(
                "The ephemeral public key cannot be represented as a JSON Web Key."
            );
        var jwk = exported.Jwk.AsElement();

        var epk = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var member in jwkKeyType.ThumbprintMembers)
        {
            // RFC 7518 §4.6.1.1: the ephemeral private key never leaves the sender.
            if (jwkKeyType.PrivateMembers.Contains(member))
            {
                throw new JoseException(
                    $"The key type '{jwkKeyType.Name}' lists the private member '{member}' among its required members."
                );
            }

            if (!jwk.TryGetProperty(member, out var value))
            {
                throw new JoseException(
                    $"The ephemeral public key's JSON Web Key is missing the '{member}' member."
                );
            }

            epk[member] =
                value.ValueKind == JsonValueKind.String && value.GetString() is { } text
                    ? text
                    : value.Clone();
        }

        return epk;
    }

    private static SecretKey ReadEphemeralKey(
        ISecretKeyTypeDescriptor descriptor,
        JsonWebKeyTypeDescriptor jwkKeyType,
        JsonElement epk
    )
    {
        if (epk.ValueKind != JsonValueKind.Object)
        {
            throw new JoseException("The 'epk' header must be a JSON object.");
        }

        if (!epk.TryGetPropertyValue<string>(JoseClaimNames.Header.Kty, out var kty))
        {
            throw new JoseException("The 'epk' header is missing the 'kty' field.");
        }

        if (!string.Equals(kty, jwkKeyType.Name, StringComparison.Ordinal))
        {
            throw new JoseException($"The 'kty' field was expected to be '{jwkKeyType.Name}'.");
        }

        // RFC 7518 §4.6.1.1: an ephemeral key that carries its private key has disclosed the content encryption key to
        // anyone holding the token, so the content is no longer confidential.
        foreach (var privateMember in jwkKeyType.PrivateMembers)
        {
            if (epk.TryGetProperty(privateMember, out _))
            {
                throw new JoseException(
                    $"The 'epk' header must contain only public key parameters, but it contains '{privateMember}'."
                );
            }
        }

        // The private members of every standard key type are refused too, so a contributed key type fails closed.
        if (new JsonWebKey(epk, jwkKeyType).ContainsPrivateKeyMaterial)
        {
            throw new JoseException("The 'epk' header must contain only public key parameters.");
        }

        // Only the key type's required members are imported; any other member is ignored (RFC 7518 §4.6.1.1).
        using var minimalBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        using (var writer = new Utf8JsonWriter(minimalBuffer))
        {
            writer.WriteStartObject();
            foreach (var member in jwkKeyType.ThumbprintMembers)
            {
                if (!epk.TryGetProperty(member, out var value))
                {
                    throw new JoseException($"The 'epk' header is missing the '{member}' field.");
                }

                writer.WritePropertyName(member);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var minimalJwk = minimalBuffer.GetSpanLease(isSensitive: false);

        try
        {
            var imported = descriptor.ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = SecureEncoding.UTF8.GetString(minimalJwk.Span),
                },
                // The material is a public key, so it is carried rather than protected.
                static publicKey =>
                    Convert.ToBase64String(publicKey)
            );

            return descriptor.DeserializeKeyMaterial(
                default,
                imported.SecretEncoding,
                Convert.FromBase64String(imported.ProtectedKeyMaterial)
            );
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or NotSupportedException
                        or CryptographicException
                        or FormatException
            )
        {
            throw new JoseException(
                $"The 'epk' header is not a valid '{jwkKeyType.Name}' public key.",
                exception
            );
        }
    }

    /// <summary>
    /// Derives the key with the Concat KDF of NIST SP 800-56A §5.8.1 (RFC 7518 §4.6.2):
    /// <c>K(i) = SHA-256(counter(i) || Z || OtherInfo)</c>.
    /// </summary>
    private static void DeriveKey(
        string algorithm,
        string? apu,
        string? apv,
        ReadOnlySpan<byte> sharedSecret,
        Span<byte> destination
    )
    {
        var keySizeBits = destination.Length << 3;

        // RFC 7518 §4.6.2: the Concat KDF Digest Method is SHA-256 for EVERY curve — the digest is fixed by the JWE
        // algorithm, not chosen from the key's curve. Selecting it from the curve size instead (SHA-384 for P-384,
        // SHA-512 for P-521) is what the Windows CNG SP800_56A_CONCAT KDF does, and it derives a different content
        // encryption key than every conforming JOSE implementation on those curves.
        const int hashSizeBytes = SHA256.HashSizeInBytes;

        var otherInfo = GetOtherInfo(algorithm, apu, apv, keySizeBits);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> counter = stackalloc byte[sizeof(int)];
        Span<byte> block = stackalloc byte[hashSizeBytes];

        try
        {
            for (var rep = 1; destination.Length > 0; ++rep)
            {
                BinaryPrimitives.WriteInt32BigEndian(counter, rep);
                hash.AppendData(counter);
                hash.AppendData(sharedSecret);
                hash.AppendData(otherInfo);

                var written = hash.GetHashAndReset(block);
                Debug.Assert(written == hashSizeBytes);

                var partialLength = Math.Min(hashSizeBytes, destination.Length);
                block[..partialLength].CopyTo(destination);
                destination = destination[partialLength..];
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
        }
    }

    private static byte[] GetOtherInfo(string algorithm, string? apu, string? apv, int keySizeBits)
    {
        var apuBytes = DecodePartyInfo(JoseClaimNames.Header.Apu, apu);
        var apvBytes = DecodePartyInfo(JoseClaimNames.Header.Apv, apv);
        var algorithmByteCount = SecureEncoding.ASCII.GetByteCount(algorithm);

        var bytes = GC.AllocateUninitializedArray<byte>(
            // AlgorithmID: length prefix and ASCII bytes
            sizeof(int)
                + algorithmByteCount
                // PartyUInfo: length prefix and bytes
                + sizeof(int)
                + apuBytes.Length
                // PartyVInfo: length prefix and bytes
                + sizeof(int)
                + apvBytes.Length
                // SuppPubInfo: key size in bits
                + sizeof(int),
            pinned: false
        );
        var pos = bytes.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(pos, algorithmByteCount);
        pos = pos[sizeof(int)..];

        var bytesWritten = SecureEncoding.ASCII.GetBytes(algorithm, pos);
        Debug.Assert(bytesWritten == algorithmByteCount);
        pos = pos[algorithmByteCount..];

        BinaryPrimitives.WriteInt32BigEndian(pos, apuBytes.Length);
        pos = pos[sizeof(int)..];

        apuBytes.CopyTo(pos);
        pos = pos[apuBytes.Length..];

        BinaryPrimitives.WriteInt32BigEndian(pos, apvBytes.Length);
        pos = pos[sizeof(int)..];

        apvBytes.CopyTo(pos);
        pos = pos[apvBytes.Length..];

        BinaryPrimitives.WriteInt32BigEndian(pos, keySizeBits);

        return bytes;
    }

    private static byte[] DecodePartyInfo(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        // A value that is present but malformed fails closed rather than being read as empty.
        try
        {
            return Base64Url.Decode(value);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new JoseException($"The '{name}' header is not valid base64url.", exception);
        }
    }

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override bool SupportsKeyWrapping => !IsDirectAgreement;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; }

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; }

    private ImmutableArray<IEcdhKeyAgreement> KeyAgreements { get; }

    private bool IsDirectAgreement { get; }

    private string AlgorithmField { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhKeyManagementAlgorithm"/> class
    /// for usage in direct key agreement mode (<c>ECDH-ES</c>).
    /// </summary>
    /// <param name="secretKeyTypeCatalog">The registered key types, whose descriptors write and read the
    /// <c>epk</c>.</param>
    /// <param name="keyAgreements">The registered key agreements; a key is agreed by the one agreement that accepts
    /// it.</param>
    public EcdhKeyManagementAlgorithm(
        ISecretKeyTypeCatalog secretKeyTypeCatalog,
        IEnumerable<IEcdhKeyAgreement> keyAgreements
    )
        : this(
            secretKeyTypeCatalog,
            keyAgreements,
            AlgorithmCodes.KeyManagement.EcdhEs,
            isDirectAgreement: true
        )
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="secretKeyTypeCatalog">The registered key types, whose descriptors write and read the
    /// <c>epk</c>.</param>
    /// <param name="keyAgreements">The registered key agreements; a key is agreed by the one agreement that accepts
    /// it.</param>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic
    /// algorithm.</param>
    /// <param name="isDirectAgreement"><c>true</c> if the algorithm is to use direct key agreement mode;
    /// otherwise, <c>false</c> if the algorithm is to use key agreement with key wrapping mode.</param>
    protected EcdhKeyManagementAlgorithm(
        ISecretKeyTypeCatalog secretKeyTypeCatalog,
        IEnumerable<IEcdhKeyAgreement> keyAgreements,
        string code,
        bool isDirectAgreement
    )
    {
        SecretKeyTypeCatalog = secretKeyTypeCatalog;
        KeyAgreements = [.. keyAgreements];
        Code = code;
        IsDirectAgreement = isDirectAgreement;
        AlgorithmField = isDirectAgreement ? JoseClaimNames.Header.Enc : JoseClaimNames.Header.Alg;
        KeyTypes = MergeKeyTypes(KeyAgreements);
    }

    /// <inheritdoc />
    public override bool IsKeyCompatible(SecretKey secretKey) =>
        base.IsKeyCompatible(secretKey) && TryGetKeyAgreement(secretKey, out _, out _);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        StaticCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => 0;

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey
    ) => AgreeAndDeriveKey(secretKey, header, contentKey);

    /// <summary>
    /// The sender's half of the agreement: agrees a shared secret with the recipient's key through an ephemeral key,
    /// writes the ephemeral public key to the header as the <c>epk</c>, and derives the key with the Concat KDF
    /// (RFC 7518 §4.6.2). In direct key agreement mode the derived key is the content encryption key; in key agreement
    /// with key wrapping mode it is the key encryption key.
    /// </summary>
    /// <param name="secretKey">The recipient's key.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation, which receives the
    /// <c>epk</c>.</param>
    /// <param name="destination">The destination for the derived key, whose length is the key's size.</param>
    protected void AgreeAndDeriveKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> destination
    )
    {
        var (keyAgreement, descriptor, jwkKeyType) = Resolve(secretKey);

        if (!header.TryGetValue<string>(AlgorithmField, out var algorithm))
        {
            throw new JoseException($"The JWT header is missing the '{AlgorithmField}' field.");
        }

        header.TryGetValue<string>(JoseClaimNames.Header.Apu, out var apu);
        header.TryGetValue<string>(JoseClaimNames.Header.Apv, out var apv);

        // The shared secret's size is the agreement's, which a package contributes, so it is a sensitive rental, zeroed
        // when it is returned.
        using var sharedSecretLease = BufferFactory.Rent(
            keyAgreement.GetSharedSecretSizeBytes(secretKey),
            isSensitive: true,
            out Span<byte> sharedSecret
        );

        using var ephemeralPublicKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: false
        );
        keyAgreement.AgreeWithEphemeralKey(secretKey, ephemeralPublicKeyBuffer, sharedSecret);

        using var ephemeralPublicKey = ephemeralPublicKeyBuffer.GetSpanLease(isSensitive: false);
        header[JoseClaimNames.Header.Epk] = WriteEphemeralKey(
            descriptor,
            jwkKeyType,
            ephemeralPublicKey.Span
        );

        DeriveKey(algorithm, apu, apv, sharedSecret, destination);
    }

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        throw new JoseException(
            "The 'ECDH-ES' key management algorithm does not support using an existing CEK."
        );
    }

    /// <inheritdoc />
    public override void WrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        NewKey(secretKey, header, contentKey);
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    )
    {
        if (secretKey is IRemoteSecretKey)
            throw new JoseInvalidAlgorithmException(
                "A key that a key management service holds unwraps a content key only through UnwrapKeyAsync."
            );

        if (encryptedContentKey.Length != 0)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey)
            );
        }

        ReceiveAndDeriveKey(secretKey, header, contentKey);

        bytesWritten = contentKey.Length;
        return true;
    }

    /// <inheritdoc />
    // See ADR-1477.
    public override ValueTask<int> UnwrapKeyAsync(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlyMemory<byte> encryptedContentKey,
        Memory<byte> contentKey,
        CancellationToken cancellationToken
    )
    {
        if (secretKey is not IRemoteSecretKey remoteKey)
            return base.UnwrapKeyAsync(
                secretKey,
                header,
                encryptedContentKey,
                contentKey,
                cancellationToken
            );

        if (encryptedContentKey.Length != 0)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey)
            );
        }

        return UnwrapWithRemoteKeyAsync(
            secretKey,
            remoteKey,
            header,
            contentKey,
            cancellationToken
        );
    }

    private async ValueTask<int> UnwrapWithRemoteKeyAsync(
        SecretKey secretKey,
        IRemoteSecretKey remoteKey,
        JsonElement header,
        Memory<byte> contentKey,
        CancellationToken cancellationToken
    )
    {
        await ReceiveAndDeriveRemoteKeyAsync(
            secretKey,
            remoteKey,
            header,
            contentKey,
            cancellationToken
        );

        return contentKey.Length;
    }

    /// <summary>
    /// The recipient's half of the agreement: agrees the shared secret from the recipient's private key and the
    /// <c>epk</c> the header carries, and derives the key with the Concat KDF (RFC 7518 §4.6.2), the same key
    /// <see cref="AgreeAndDeriveKey"/> derived for the sender.
    /// </summary>
    /// <param name="secretKey">The recipient's private key.</param>
    /// <param name="header">The JOSE header of the token, which carries the <c>epk</c>.</param>
    /// <param name="destination">The destination for the derived key, whose length is the key's size.</param>
    protected void ReceiveAndDeriveKey(
        SecretKey secretKey,
        JsonElement header,
        Span<byte> destination
    )
    {
        var keyAgreement = ResolveKeyAgreement(secretKey);
        var ephemeralPublicKey = ValidateHeaderForUnwrap(
            secretKey,
            header,
            out var algorithm,
            out var apu,
            out var apv
        );

        using var sharedSecretLease = BufferFactory.Rent(
            keyAgreement.GetSharedSecretSizeBytes(secretKey),
            isSensitive: true,
            out Span<byte> sharedSecret
        );

        keyAgreement.Agree(secretKey, ephemeralPublicKey, sharedSecret);

        DeriveKey(algorithm, apu, apv, sharedSecret, destination);
    }

    /// <summary>
    /// The recipient's half of the agreement for a key that a key management service holds: the service agrees the
    /// shared secret from its private key and the <c>epk</c> the header carries, and the Concat KDF
    /// (RFC 7518 §4.6.2) derives the same key <see cref="AgreeAndDeriveKey"/> derived for the sender.
    /// </summary>
    /// <param name="secretKey">The recipient's key.</param>
    /// <param name="remoteKey">The recipient's key as the key that the service holds.</param>
    /// <param name="header">The JOSE header of the token, which carries the <c>epk</c>.</param>
    /// <param name="destination">The destination for the derived key, whose length is the key's size.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    // See ADR-1477.
    protected async ValueTask ReceiveAndDeriveRemoteKeyAsync(
        SecretKey secretKey,
        IRemoteSecretKey remoteKey,
        JsonElement header,
        Memory<byte> destination,
        CancellationToken cancellationToken
    )
    {
        var keyAgreement = ResolveKeyAgreement(secretKey);
        var ephemeralPublicKey = ValidateHeaderForUnwrap(
            secretKey,
            header,
            out var algorithm,
            out var apu,
            out var apv
        );

        using var ephemeralDisposal = ephemeralPublicKey as IDisposable;

        if (
            secretKey is not AsymmetricSecretKey { CurveName: { } recipientCurveName }
            || ephemeralPublicKey
                is not AsymmetricSecretKey { CurveName: { } ephemeralCurveName } ephemeralKey
            || !string.Equals(recipientCurveName, ephemeralCurveName, StringComparison.Ordinal)
        )
        {
            throw new JoseException(
                "The ephemeral public key is not on the curve of the recipient's key."
            );
        }

        var peerSubjectPublicKeyInfo = ExportSubjectPublicKeyInfo(ephemeralKey);

        using var sharedSecretLease = BufferFactory.Rent(
            keyAgreement.GetSharedSecretSizeBytes(secretKey),
            isSensitive: true,
            out Memory<byte> sharedSecret
        );

        var written = await remoteKey.DeriveSharedSecretAsync(
            Code,
            peerSubjectPublicKeyInfo,
            sharedSecret,
            cancellationToken
        );

        if (written != sharedSecret.Length)
        {
            throw new CryptographicException(
                "The key management service returned a shared secret that is not the size of the curve's field."
            );
        }

        DeriveKey(algorithm, apu, apv, sharedSecret.Span, destination.Span);
    }

    private static byte[] ExportSubjectPublicKeyInfo(AsymmetricSecretKey publicKey)
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        IBufferWriter<byte> writer = buffer;
        publicKey.ExportSubjectPublicKeyInfo(ref writer);
        using var lease = buffer.GetSpanLease(isSensitive: false);
        return lease.Span.ToArray();
    }

    /// <summary>
    /// Reads the algorithm, the <c>apu</c> and <c>apv</c> parameters and the ephemeral public key from the JOSE header
    /// for the specified recipient key. The <c>epk</c> must be a JSON Web Key of the recipient key's type that
    /// carries only public members: its <c>kty</c> must be that type's, it must not carry any private member, and only
    /// the type's required members are imported, through the type's descriptor, which validates the key.
    /// </summary>
    internal SecretKey ValidateHeaderForUnwrap(
        SecretKey recipientKey,
        JsonElement header,
        out string algorithm,
        out string? apu,
        out string? apv
    )
    {
        var (_, descriptor, jwkKeyType) = Resolve(recipientKey);

        if (!header.TryGetPropertyValue<string>(AlgorithmField, out var localAlgorithm))
        {
            throw new JoseException($"The JWT header is missing the '{AlgorithmField}' field.");
        }

        if (!header.TryGetPropertyValue<JsonElement>(JoseClaimNames.Header.Epk, out var epk))
        {
            throw new JoseException("The JWT header is missing the 'epk' field.");
        }

        var ephemeralPublicKey = ReadEphemeralKey(descriptor, jwkKeyType, epk);

        algorithm = localAlgorithm;

        header.TryGetPropertyValue(JoseClaimNames.Header.Apu, out apu);
        header.TryGetPropertyValue(JoseClaimNames.Header.Apv, out apv);

        return ephemeralPublicKey;
    }

    /// <summary>
    /// Finds the one agreement that accepts the key: several agreements may serve one key type, each over its own
    /// curves, so the agreement is chosen by the key rather than by its type.
    /// </summary>
    // See ADR-1389.
    private bool TryGetKeyAgreement(
        SecretKey secretKey,
        [NotNullWhen(true)] out IEcdhKeyAgreement? keyAgreement,
        [NotNullWhen(false)] out string? error
    )
    {
        var keyType = secretKey.GetType();
        keyAgreement = null;
        var servesKeyType = false;
        foreach (var candidate in KeyAgreements)
        {
            if (!candidate.AlgorithmKeyType.Accepts(keyType))
            {
                continue;
            }

            servesKeyType = true;
            if (!candidate.AlgorithmKeyType.IsKeyCompatible(secretKey))
            {
                continue;
            }

            if (keyAgreement is not null)
            {
                // Two agreements that both accept a key leave it ambiguous which one derives the content key.
                error =
                    $"The ECDH-ES key agreements '{keyAgreement.GetType().FullName}' and "
                    + $"'{candidate.GetType().FullName}' both accept the secret key.";
                keyAgreement = null;
                return false;
            }

            keyAgreement = candidate;
        }

        if (keyAgreement is not null)
        {
            error = null;
            return true;
        }

        error = servesKeyType
            ? "The secret key's size or curve is not valid for this cryptographic algorithm."
            : $"The secret key of type '{keyType.FullName}' has no registered ECDH key agreement.";
        return false;
    }

    private IEcdhKeyAgreement ResolveKeyAgreement(SecretKey secretKey)
    {
        if (!TryGetKeyAgreement(secretKey, out var keyAgreement, out var error))
        {
            // the argument that is invalid is the caller's secretKey, not this helper's parameter
#pragma warning disable CA2208
            throw new ArgumentException(error, nameof(secretKey));
#pragma warning restore CA2208
        }

        return keyAgreement;
    }

    private (
        IEcdhKeyAgreement KeyAgreement,
        ISecretKeyTypeDescriptor Descriptor,
        JsonWebKeyTypeDescriptor JwkKeyType
    ) Resolve(SecretKey secretKey)
    {
        var keyAgreement = ResolveKeyAgreement(secretKey);

        if (
            !SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor)
            || descriptor.JwkKeyType is not { } jwkKeyType
            || !descriptor.PublicKeyImportFormats.Contains(
                SecretKeyImportFormats.Jwk,
                StringComparer.Ordinal
            )
        )
        {
            throw new JoseException(
                $"The key type '{secretKey.KeyType}' has no registered JSON Web Key form, so it cannot carry an "
                    + "ephemeral public key."
            );
        }

        return (keyAgreement, descriptor, jwkKeyType);
    }
}
