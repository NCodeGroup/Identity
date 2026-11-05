// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Messages;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose;

internal partial class DefaultCoseSerializer
{
    /// <inheritdoc />
    public byte[] EncodeEncrypt0(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);

        var (_, algorithm) = GetEncryptionBinding(credentials.Algorithm);
        if (
            credentials.SecretKey is not SymmetricSecretKey key
            || key.KeySizeBytes != algorithm.ContentKeySizeBytes
        )
            throw new ArgumentException(
                "The key is not exactly the size the content encryption algorithm needs.",
                nameof(credentials)
            );

        var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            key,
            credentials.Algorithm.Identifier
        );
        var protectedBytes = CborMapWriter.EncodeProtected(protectedHeaders);

        var nonce = new byte[algorithm.NonceSizeBytes];
        RandomSource.Fill(nonce);
        unprotectedHeaders[CoseHeaderLabels.InitializationVector] = CoseValue.FromByteString(nonce);

        using var keyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: true,
            key.KeySizeBytes
        );
        IBufferWriter<byte> keyWriter = keyBuffer;
        key.ExportPrivateKey(ref keyWriter);
        using var keyLease = keyBuffer.GetSpanLease(isSensitive: true);

        var ciphertext = Encrypt(
            algorithm,
            keyLease.Span,
            nonce,
            content,
            CoseStructures.Encrypt("Encrypt0", protectedBytes, options.ExternalAad.Span)
        );

        return CoseMessageWriter.Encrypt0(
            options.OmitTag ? null : CoseTags.Encrypt0,
            protectedBytes,
            unprotectedHeaders,
            CarriedContent(ciphertext, options)
        );
    }

    /// <inheritdoc />
    public byte[] EncodeEncrypt(
        ReadOnlySpan<byte> content,
        CoseRecipients recipients,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(options);

        var (_, algorithm) = GetEncryptionBinding(recipients.ContentAlgorithm);
        var algorithms = GetRecipientAlgorithms(recipients);

        var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            null,
            recipients.ContentAlgorithm.Identifier
        );
        var protectedBytes = CborMapWriter.EncodeProtected(protectedHeaders);

        using var lease = BufferFactory.Rent(
            algorithm.ContentKeySizeBytes,
            isSensitive: true,
            out Span<byte> contentKey
        );
        List<CoseLayerEntry> entries;
        if (!algorithms[0].SupportsKeyWrapping)
        {
            var recipient = recipients.Recipients[0];
            if (
                recipient.Credentials.SecretKey is not SymmetricSecretKey direct
                || direct.KeySizeBytes != algorithm.ContentKeySizeBytes
            )
                throw new ArgumentException(
                    "The direct recipient key is not exactly the size the content encryption algorithm needs.",
                    nameof(recipients)
                );

            algorithms[0].NewKey(direct, new Dictionary<string, object>(), contentKey);
            entries = [NewRecipientEntry(recipient, [])];
        }
        else
        {
            RandomSource.Fill(contentKey);
            entries = WrapForRecipients(recipients, algorithms, contentKey);
        }

        var nonce = new byte[algorithm.NonceSizeBytes];
        RandomSource.Fill(nonce);
        unprotectedHeaders[CoseHeaderLabels.InitializationVector] = CoseValue.FromByteString(nonce);

        var ciphertext = Encrypt(
            algorithm,
            contentKey,
            nonce,
            content,
            CoseStructures.Encrypt("Encrypt", protectedBytes, options.ExternalAad.Span)
        );

        return CoseMessageWriter.Encrypt(
            options.OmitTag ? null : CoseTags.Encrypt,
            protectedBytes,
            unprotectedHeaders,
            CarriedContent(ciphertext, options),
            entries
        );
    }

    /// <inheritdoc />
    public CoseVerifiedMessage Decrypt(
        ReadOnlyMemory<byte> message,
        CoseDecryptionParameters parameters
    )
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.ValidAlgorithms);
        ArgumentNullException.ThrowIfNull(parameters.Keys);

        if (parameters.ValidAlgorithms.Count == 0)
            throw new ArgumentException(
                "At least one valid algorithm must be named.",
                nameof(parameters)
            );

        if (parameters.ClockSkew < TimeSpan.Zero)
            throw new ArgumentException("The clock skew must not be negative.", nameof(parameters));

        var parsed = CoseMessageReader.Read(message, parameters.UntaggedMessageType, Options);
        if (parsed.MessageType is not (CoseMessageType.Encrypt0 or CoseMessageType.Encrypt))
            throw new CoseException(
                CoseFailureCodes.UnsupportedMessageType,
                "The message is not a type that can be decrypted."
            );

        var ciphertext = ResolveContent(parsed, parameters.DetachedContent);
        CoseHeaderPolicy.EnsureCriticalUnderstood(
            parsed.Body.Protected,
            parameters.UnderstoodCriticalHeaders
        );
        var binding = CoseHeaderPolicy.ResolveLayerAlgorithm(
            parsed.Body,
            parameters.ValidAlgorithms,
            CoseAlgorithmKind.ContentEncryption,
            AlgorithmCatalog
        );
        var algorithm = (AuthenticatedEncryptionAlgorithm)binding.JoseAlgorithm;

        var nonce = ReadNonce(parsed.Body, algorithm);
        if (ciphertext.Length < algorithm.AuthenticationTagSizeBytes)
            throw new CoseException(
                CoseFailureCodes.DecryptionFailed,
                "The ciphertext is shorter than its authentication tag."
            );

        var isEncrypt0 = parsed.MessageType == CoseMessageType.Encrypt0;
        var structure = CoseStructures.Encrypt(
            isEncrypt0 ? "Encrypt0" : "Encrypt",
            parsed.Body.EffectiveProtected.Span,
            parameters.ExternalAad.Span
        );

        if (isEncrypt0)
            return DecryptEncrypt0(
                parsed,
                parameters,
                binding,
                algorithm,
                nonce,
                ciphertext,
                structure
            );

        return DecryptEncrypt(parsed, parameters, binding, algorithm, nonce, ciphertext, structure);
    }

    private CoseVerifiedMessage DecryptEncrypt0(
        ParsedCoseMessage parsed,
        CoseDecryptionParameters parameters,
        CoseAlgorithmBinding binding,
        AuthenticatedEncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> ciphertext,
        byte[] structure
    )
    {
        var candidates = SelectKeys(
            parsed.Body,
            algorithm.Code,
            key => key is SymmetricSecretKey && key.KeySizeBytes == algorithm.ContentKeySizeBytes,
            parameters.Keys,
            SecretKeyUses.Encryption,
            parameters.ClockSkew
        );
        if (candidates.Count == 0)
            throw new CoseException(
                CoseFailureCodes.KeyNotFound,
                "No key can decrypt the message."
            );

        foreach (var candidate in candidates)
        {
            using var keyBuffer = BufferFactory.CreatePooledBufferWriter(
                isSensitive: true,
                candidate.KeySizeBytes
            );
            IBufferWriter<byte> keyWriter = keyBuffer;
            ((SymmetricSecretKey)candidate).ExportPrivateKey(ref keyWriter);
            using var keyLease = keyBuffer.GetSpanLease(isSensitive: true);

            if (
                TryDecrypt(algorithm, keyLease.Span, nonce.Span, ciphertext.Span, structure) is
                { } plaintext
            )
                return new CoseVerifiedMessage
                {
                    MessageType = CoseMessageType.Encrypt0,
                    Content = plaintext,
                    ProtectedHeaders = parsed.Body.Protected,
                    UnprotectedHeaders = parsed.Body.Unprotected,
                    Algorithm = binding.Algorithm,
                    Key = candidate,
                };
        }

        throw new CoseException(CoseFailureCodes.DecryptionFailed, "The message does not decrypt.");
    }

    private CoseVerifiedMessage DecryptEncrypt(
        ParsedCoseMessage parsed,
        CoseDecryptionParameters parameters,
        CoseAlgorithmBinding binding,
        AuthenticatedEncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> ciphertext,
        byte[] structure
    )
    {
        var eligible = ResolveEligibleRecipients(
            parsed.Recipients,
            parameters.ValidKeyDistributionAlgorithms,
            out var skipped
        );
        if (eligible.Count == 0)
            throw new CoseException(
                skipped == CoseRecipientSkip.NotPermitted
                    ? CoseFailureCodes.AlgorithmNotPermitted
                    : CoseFailureCodes.UnsupportedAlgorithm,
                "No recipient of the message uses a permitted algorithm."
            );

        var sawCandidate = false;
        foreach (var (recipient, recipientBinding) in eligible)
        {
            var keyDistribution = (KeyManagementAlgorithm)recipientBinding.JoseAlgorithm;
            var isDirect = !keyDistribution.SupportsKeyWrapping;
            if (isDirect && recipient.Third.Length != 0)
                throw CborGuard.Invalid("A direct recipient carries ciphertext.");

            var candidates = isDirect
                ? SelectKeys(
                    recipient,
                    algorithm.Code,
                    key =>
                        key is SymmetricSecretKey
                        && key.KeySizeBytes == algorithm.ContentKeySizeBytes,
                    parameters.Keys,
                    SecretKeyUses.Encryption,
                    parameters.ClockSkew
                )
                : SelectKeys(
                    recipient,
                    keyDistribution.Code,
                    keyDistribution.IsKeyCompatible,
                    parameters.Keys,
                    SecretKeyUses.Encryption,
                    parameters.ClockSkew
                );
            sawCandidate |= candidates.Count > 0;

            foreach (var candidate in candidates)
            {
                using var lease = BufferFactory.Rent(
                    Math.Max(recipient.Third.Length, algorithm.ContentKeySizeBytes),
                    isSensitive: true,
                    out Span<byte> contentKey
                );
                if (
                    !CoseContentKeys.TryUnwrap(
                        keyDistribution,
                        candidate,
                        recipient.Third.Span,
                        contentKey,
                        out var written
                    )
                    || written != algorithm.ContentKeySizeBytes
                )
                    continue;

                if (
                    TryDecrypt(
                        algorithm,
                        contentKey[..written],
                        nonce.Span,
                        ciphertext.Span,
                        structure
                    ) is
                    { } plaintext
                )
                    return new CoseVerifiedMessage
                    {
                        MessageType = CoseMessageType.Encrypt,
                        Content = plaintext,
                        ProtectedHeaders = parsed.Body.Protected,
                        UnprotectedHeaders = parsed.Body.Unprotected,
                        Algorithm = binding.Algorithm,
                        Key = candidate,
                        Recipient = new CoseLayerHeaders
                        {
                            ProtectedHeaders = recipient.Protected,
                            UnprotectedHeaders = recipient.Unprotected,
                        },
                    };
            }
        }

        throw new CoseException(
            sawCandidate ? CoseFailureCodes.DecryptionFailed : CoseFailureCodes.KeyNotFound,
            sawCandidate ? "The message does not decrypt." : "No key can decrypt the message."
        );
    }

    private (
        CoseAlgorithmBinding Binding,
        AuthenticatedEncryptionAlgorithm Algorithm
    ) GetEncryptionBinding(CoseAlgorithm algorithm)
    {
        if (
            !AlgorithmCatalog.TryGet(algorithm.Identifier, out var binding)
            || binding.Algorithm.Kind != CoseAlgorithmKind.ContentEncryption
            || binding.JoseAlgorithm is not AuthenticatedEncryptionAlgorithm aead
        )
            throw new ArgumentException(
                "The algorithm is not a registered COSE content encryption algorithm.",
                nameof(algorithm)
            );

        return (binding, aead);
    }

    private static ReadOnlyMemory<byte> ReadNonce(
        ParsedCoseLayer body,
        AuthenticatedEncryptionAlgorithm algorithm
    )
    {
        if (
            (
                body.Protected.TryGetValue(CoseHeaderLabels.InitializationVector, out var value)
                || body.Unprotected.TryGetValue(CoseHeaderLabels.InitializationVector, out value)
            )
            && value.TryGetByteString(out var nonce)
            && nonce.Length == algorithm.NonceSizeBytes
        )
            return nonce;

        throw new CoseException(
            CoseFailureCodes.InvalidHeader,
            "The message has no initialization vector of the size its algorithm needs."
        );
    }

    private static byte[] Encrypt(
        AuthenticatedEncryptionAlgorithm algorithm,
        ReadOnlySpan<byte> contentKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        byte[] structure
    )
    {
        var cipherTextSize = algorithm.GetCipherTextSizeBytes(plaintext.Length);
        var output = new byte[cipherTextSize + algorithm.AuthenticationTagSizeBytes];
        algorithm.Encrypt(
            contentKey,
            nonce,
            plaintext,
            structure,
            output.AsSpan(0, cipherTextSize),
            output.AsSpan(cipherTextSize)
        );

        return output;
    }

    private static byte[]? TryDecrypt(
        AuthenticatedEncryptionAlgorithm algorithm,
        ReadOnlySpan<byte> contentKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertextAndTag,
        byte[] structure
    )
    {
        var cipherTextSize = ciphertextAndTag.Length - algorithm.AuthenticationTagSizeBytes;
        var plaintext = new byte[algorithm.GetMaxPlainTextSizeBytes(cipherTextSize)];
        try
        {
            if (
                !algorithm.TryDecrypt(
                    contentKey,
                    nonce,
                    ciphertextAndTag[..cipherTextSize],
                    structure,
                    ciphertextAndTag[cipherTextSize..],
                    plaintext,
                    out var written
                )
            )
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return null;
            }

            // The buffer is the content when the plaintext fills it, as it does for AES-GCM.
            if (written == plaintext.Length)
                return plaintext;

            var content = plaintext.AsSpan(0, written).ToArray();
            CryptographicOperations.ZeroMemory(plaintext);
            return content;
        }
        catch (Exception exception)
            when (exception is JoseException or CryptographicException or ArgumentException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return null;
        }
    }
}
