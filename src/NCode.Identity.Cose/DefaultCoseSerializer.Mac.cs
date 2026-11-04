// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cose.Messages;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose;

internal partial class DefaultCoseSerializer
{
    /// <inheritdoc />
    public byte[] EncodeMac0(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);

        var binding = GetMacBinding(credentials.Algorithm);
        if (
            credentials.SecretKey is not SymmetricSecretKey
            || !((SignatureAlgorithm)binding.JoseAlgorithm).IsKeyCompatible(credentials.SecretKey)
        )
            throw new ArgumentException(
                "The key does not suit the message authentication code algorithm.",
                nameof(credentials)
            );

        var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            credentials.SecretKey,
            credentials.Algorithm.Identifier
        );
        var protectedBytes = CborMapWriter.EncodeProtected(protectedHeaders);
        var structure = CoseStructures.Mac(
            "MAC0",
            protectedBytes,
            options.ExternalAad.Span,
            content
        );

        return CoseMessageWriter.Mac0(
            options.OmitTag ? null : CoseTags.Mac0,
            protectedBytes,
            unprotectedHeaders,
            CarriedContent(content, options),
            ComputeTag(binding, credentials.SecretKey, structure)
        );
    }

    /// <inheritdoc />
    public byte[] EncodeMac(
        ReadOnlySpan<byte> content,
        CoseRecipients recipients,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(options);

        var binding = GetMacBinding(recipients.ContentAlgorithm);
        var macAlgorithm = (SignatureAlgorithm)binding.JoseAlgorithm;
        var algorithms = GetRecipientAlgorithms(recipients);

        var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            null,
            recipients.ContentAlgorithm.Identifier
        );
        var protectedBytes = CborMapWriter.EncodeProtected(protectedHeaders);

        SecretKey macKey;
        List<CoseMessageWriter.LayerEntry> entries;
        if (!algorithms[0].SupportsKeyWrapping)
        {
            macKey = recipients.Recipients[0].Credentials.SecretKey;
            if (macKey is not SymmetricSecretKey || !macAlgorithm.IsKeyCompatible(macKey))
                throw new ArgumentException(
                    "The direct recipient key does not suit the message authentication code algorithm.",
                    nameof(recipients)
                );

            entries = [NewRecipientEntry(recipients.Recipients[0], [])];
        }
        else
        {
            var contentKeySize = macAlgorithm.HashAlgorithmName.GetHashSizeBits() >> 3;
            using var lease = BufferFactory.Rent(
                contentKeySize,
                isSensitive: true,
                out Span<byte> contentKey
            );
            RandomSource.Fill(contentKey);

            macKey = SecretKeyFactory.CreateSymmetric(new KeyMetadata(), contentKey);
            if (!macAlgorithm.IsKeyCompatible(macKey))
                throw new ArgumentException(
                    "The message authentication code algorithm does not accept a key of the size its hash needs.",
                    nameof(recipients)
                );

            entries = WrapForRecipients(recipients, algorithms, contentKey);
        }

        var structure = CoseStructures.Mac(
            "MAC",
            protectedBytes,
            options.ExternalAad.Span,
            content
        );
        return CoseMessageWriter.Mac(
            options.OmitTag ? null : CoseTags.Mac,
            protectedBytes,
            unprotectedHeaders,
            CarriedContent(content, options),
            ComputeTag(binding, macKey, structure),
            entries
        );
    }

    private CoseVerifiedMessage VerifyMac0(
        ParsedCoseMessage parsed,
        CoseVerificationParameters parameters
    )
    {
        var payload = ResolveContent(parsed, parameters.DetachedContent);
        CoseHeaderPolicy.EnsureCriticalUnderstood(
            parsed.Body.Protected,
            parameters.UnderstoodCriticalHeaders
        );
        var binding = CoseHeaderPolicy.ResolveLayerAlgorithm(
            parsed.Body,
            parameters.ValidAlgorithms,
            CoseAlgorithmKind.Mac,
            AlgorithmCatalog
        );
        var structure = CoseStructures.Mac(
            "MAC0",
            parsed.Body.EffectiveProtected.Span,
            parameters.ExternalAad.Span,
            payload.Span
        );

        var candidates = SelectKeys(parsed.Body, binding, parameters, SecretKeyUses.Signature);
        if (candidates.Count == 0)
            throw new CoseException(CoseFailureCodes.KeyNotFound, "No key can verify the message.");

        foreach (var key in candidates)
        {
            if (TagMatches(binding, key, structure, parsed.Authenticator.Span))
                return new CoseVerifiedMessage
                {
                    MessageType = CoseMessageType.Mac0,
                    Content = payload,
                    ProtectedHeaders = parsed.Body.Protected,
                    UnprotectedHeaders = parsed.Body.Unprotected,
                    Algorithm = binding.Algorithm,
                    Key = key,
                };
        }

        throw new CoseException(
            CoseFailureCodes.InvalidSignature,
            "The message authentication code does not verify."
        );
    }

    private CoseVerifiedMessage VerifyMac(
        ParsedCoseMessage parsed,
        CoseVerificationParameters parameters
    )
    {
        var payload = ResolveContent(parsed, parameters.DetachedContent);
        CoseHeaderPolicy.EnsureCriticalUnderstood(
            parsed.Body.Protected,
            parameters.UnderstoodCriticalHeaders
        );
        var binding = CoseHeaderPolicy.ResolveLayerAlgorithm(
            parsed.Body,
            parameters.ValidAlgorithms,
            CoseAlgorithmKind.Mac,
            AlgorithmCatalog
        );
        var macAlgorithm = (SignatureAlgorithm)binding.JoseAlgorithm;
        var structure = CoseStructures.Mac(
            "MAC",
            parsed.Body.EffectiveProtected.Span,
            parameters.ExternalAad.Span,
            payload.Span
        );

        var eligible = ResolveEligibleRecipients(
            parsed.Recipients,
            parameters.ValidKeyDistributionAlgorithms,
            out var skipped
        );
        if (eligible.Count == 0)
            throw new CoseException(
                skipped == CoseContentKeys.RecipientSkip.NotPermitted
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
                    macAlgorithm.Code,
                    key => key is SymmetricSecretKey && macAlgorithm.IsKeyCompatible(key),
                    parameters.Keys,
                    SecretKeyUses.Signature,
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
                if (
                    TryGetMacKey(
                        macAlgorithm,
                        keyDistribution,
                        candidate,
                        recipient,
                        out var macKey
                    ) && TagMatches(binding, macKey, structure, parsed.Authenticator.Span)
                )
                    return new CoseVerifiedMessage
                    {
                        MessageType = CoseMessageType.Mac,
                        Content = payload,
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
            sawCandidate ? CoseFailureCodes.InvalidSignature : CoseFailureCodes.KeyNotFound,
            sawCandidate
                ? "The message authentication code does not verify."
                : "No key can verify the message."
        );
    }

    private bool TryGetMacKey(
        SignatureAlgorithm macAlgorithm,
        KeyManagementAlgorithm keyDistribution,
        SecretKey candidate,
        ParsedCoseLayer recipient,
        out SecretKey macKey
    )
    {
        macKey = candidate;
        if (!keyDistribution.SupportsKeyWrapping)
            return true;

        using var lease = BufferFactory.Rent(
            Math.Max(recipient.Third.Length, 1),
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
            || written == 0
        )
            return false;

        macKey = SecretKeyFactory.CreateSymmetric(new KeyMetadata(), contentKey[..written]);
        return macAlgorithm.IsKeyCompatible(macKey);
    }

    private List<(
        ParsedCoseLayer Recipient,
        CoseAlgorithmBinding Binding
    )> ResolveEligibleRecipients(
        IReadOnlyList<ParsedCoseLayer> recipients,
        IReadOnlyCollection<int> permitted,
        out CoseContentKeys.RecipientSkip skipped
    )
    {
        skipped = CoseContentKeys.RecipientSkip.Unsupported;
        var eligible = new List<(ParsedCoseLayer, CoseAlgorithmBinding)>();
        foreach (var recipient in recipients)
        {
            var binding = CoseContentKeys.ResolveRecipient(
                recipient,
                permitted,
                AlgorithmCatalog,
                out var skip
            );
            if (binding is null)
            {
                if (skip == CoseContentKeys.RecipientSkip.NotPermitted)
                    skipped = skip;

                continue;
            }

            if (
                recipients.Count > 1
                && binding.JoseAlgorithm is KeyManagementAlgorithm { SupportsKeyWrapping: false }
            )
                throw new CoseException(
                    CoseFailureCodes.InvalidHeader,
                    "A direct recipient is not the only recipient of the message."
                );

            eligible.Add((recipient, binding));
        }

        return eligible;
    }

    private CoseAlgorithmBinding GetMacBinding(CoseAlgorithm algorithm)
    {
        if (
            !AlgorithmCatalog.TryGet(algorithm.Identifier, out var binding)
            || binding.Algorithm.Kind != CoseAlgorithmKind.Mac
            || binding.JoseAlgorithm is not SignatureAlgorithm
        )
            throw new ArgumentException(
                "The algorithm is not a registered COSE message authentication code algorithm.",
                nameof(algorithm)
            );

        return binding;
    }

    private List<KeyManagementAlgorithm> GetRecipientAlgorithms(CoseRecipients recipients)
    {
        if (recipients.Recipients.Count > Options.MaxRecipients)
            throw new ArgumentException(
                "A message may carry no more recipients than the configured maximum.",
                nameof(recipients)
            );

        var algorithms = new List<KeyManagementAlgorithm>(recipients.Recipients.Count);
        foreach (var recipient in recipients.Recipients)
        {
            if (
                !CoseContentKeys.TryGetKeyDistribution(
                    recipient.Credentials,
                    AlgorithmCatalog,
                    out var algorithm
                )
            )
                throw new ArgumentException(
                    "A recipient algorithm is not a registered key distribution algorithm that suits its key.",
                    nameof(recipients)
                );

            algorithms.Add(algorithm);
        }

        if (algorithms.Any(algorithm => !algorithm.SupportsKeyWrapping) && algorithms.Count > 1)
            throw new ArgumentException(
                "A direct recipient must be the only recipient of a message.",
                nameof(recipients)
            );

        return algorithms;
    }

    private List<CoseMessageWriter.LayerEntry> WrapForRecipients(
        CoseRecipients recipients,
        List<KeyManagementAlgorithm> algorithms,
        ReadOnlySpan<byte> contentKey
    )
    {
        var entries = new List<CoseMessageWriter.LayerEntry>(algorithms.Count);
        for (var index = 0; index < algorithms.Count; index++)
        {
            var recipient = recipients.Recipients[index];
            var writer = new ArrayBufferWriter<byte>();
            algorithms[index]
                .WrapKey(
                    recipient.Credentials.SecretKey,
                    new Dictionary<string, object>(),
                    contentKey,
                    writer
                );
            entries.Add(NewRecipientEntry(recipient, writer.WrittenSpan.ToArray()));
        }

        return entries;
    }

    private CoseMessageWriter.LayerEntry NewRecipientEntry(
        CoseRecipient recipient,
        byte[] ciphertext
    )
    {
        CoseHeaderPolicy.ValidateWrite(
            FrozenDictionary<CoseLabel, CoseValue>.Empty,
            recipient.UnprotectedHeaders,
            Options.MaxNestingDepth
        );

        var unprotectedHeaders = new Dictionary<CoseLabel, CoseValue>(recipient.UnprotectedHeaders)
        {
            [CoseHeaderLabels.Algorithm] = CoseValue.FromInt64(
                recipient.Credentials.Algorithm.Identifier
            ),
        };
        if (
            recipient.Credentials.SecretKey.KeyId is { Length: > 0 } keyId
            && !unprotectedHeaders.ContainsKey(CoseHeaderLabels.KeyId)
        )
            unprotectedHeaders[CoseHeaderLabels.KeyId] = CoseValue.FromByteString(
                CoseKeyIdentifiers.GetBytes(keyId)
            );

        return new CoseMessageWriter.LayerEntry([], unprotectedHeaders, ciphertext);
    }

    private static byte[] ComputeTag(CoseAlgorithmBinding binding, SecretKey key, byte[] structure)
    {
        var algorithm = (SignatureAlgorithm)binding.JoseAlgorithm;
        var full = new byte[algorithm.GetSignatureSizeBytes(key.KeySizeBits)];
        if (!algorithm.TrySign(key, structure, full, out var written))
            throw new InvalidOperationException("The authentication code does not fit its buffer.");

        return full.AsSpan(0, binding.Algorithm.TagSizeBytes ?? written).ToArray();
    }

    private static bool TagMatches(
        CoseAlgorithmBinding binding,
        SecretKey key,
        byte[] structure,
        ReadOnlySpan<byte> received
    )
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                ComputeTag(binding, key, structure),
                received
            );
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            return false;
        }
    }
}
