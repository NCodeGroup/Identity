// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
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
    public byte[] EncodeSign1(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);

        var (algorithm, binding) = GetSigningAlgorithm(credentials);
        var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            credentials.SecretKey,
            credentials.Algorithm.Identifier
        );
        var protectedBytes = CborMapWriter.EncodeProtected(protectedHeaders);

        var structure = CoseStructures.Sign1(protectedBytes, options.ExternalAad.Span, content);
        var signature = Sign(algorithm, credentials.SecretKey, structure);

        return CoseMessageWriter.Sign1(
            options.OmitTag ? null : CoseTags.Sign1,
            protectedBytes,
            unprotectedHeaders,
            CarriedContent(content, options),
            signature
        );
    }

    /// <inheritdoc />
    public byte[] EncodeSign(
        ReadOnlySpan<byte> content,
        IReadOnlyList<CoseSigner> signers,
        CoseEncodingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(signers);
        ArgumentNullException.ThrowIfNull(options);

        if (signers.Count == 0)
            throw new ArgumentException(
                "A COSE_Sign message needs at least one signer.",
                nameof(signers)
            );

        if (signers.Count > Options.MaxSignatures)
            throw new ArgumentException(
                "A COSE_Sign message may carry no more signatures than the configured maximum.",
                nameof(signers)
            );

        var (bodyProtectedHeaders, bodyUnprotectedHeaders) = BuildHeaders(
            options.ProtectedHeaders,
            options.UnprotectedHeaders,
            null,
            null
        );
        var bodyProtected = CborMapWriter.EncodeProtected(bodyProtectedHeaders);

        var entries = new List<CoseMessageWriter.LayerEntry>(signers.Count);
        foreach (var signer in signers)
        {
            ArgumentNullException.ThrowIfNull(signer);

            var credentials = signer.Credentials;
            var (algorithm, _) = GetSigningAlgorithm(credentials);
            var (protectedHeaders, unprotectedHeaders) = BuildHeaders(
                signer.ProtectedHeaders,
                signer.UnprotectedHeaders,
                credentials.SecretKey,
                credentials.Algorithm.Identifier
            );
            var signatureProtected = CborMapWriter.EncodeProtected(protectedHeaders);
            var structure = CoseStructures.Signature(
                bodyProtected,
                signatureProtected,
                options.ExternalAad.Span,
                content
            );
            entries.Add(
                new CoseMessageWriter.LayerEntry(
                    signatureProtected,
                    unprotectedHeaders,
                    Sign(algorithm, credentials.SecretKey, structure)
                )
            );
        }

        return CoseMessageWriter.Sign(
            options.OmitTag ? null : CoseTags.Sign,
            bodyProtected,
            bodyUnprotectedHeaders,
            CarriedContent(content, options),
            entries
        );
    }

    /// <inheritdoc />
    public CoseVerifiedMessage Verify(
        ReadOnlyMemory<byte> message,
        CoseVerificationParameters parameters
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
        return parsed.MessageType switch
        {
            CoseMessageType.Sign1 => VerifySign1(parsed, parameters),
            CoseMessageType.Sign => VerifySign(parsed, parameters),
            CoseMessageType.Mac0 => VerifyMac0(parsed, parameters),
            CoseMessageType.Mac => VerifyMac(parsed, parameters),
            _ => throw new CoseException(
                CoseFailureCodes.UnsupportedMessageType,
                "The message is not a type that can be verified."
            ),
        };
    }

    private CoseVerifiedMessage VerifySign1(
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
            CoseAlgorithmKind.Signature,
            AlgorithmCatalog
        );

        var structure = CoseStructures.Sign1(
            parsed.Body.EffectiveProtected.Span,
            parameters.ExternalAad.Span,
            payload.Span
        );
        var candidates = SelectKeys(parsed.Body, binding, parameters, SecretKeyUses.Signature);
        if (candidates.Count == 0)
            throw new CoseException(CoseFailureCodes.KeyNotFound, "No key can verify the message.");

        var key =
            FindVerifyingKey(binding, candidates, structure, parsed.Authenticator.Span)
            ?? throw new CoseException(
                CoseFailureCodes.InvalidSignature,
                "The signature does not verify."
            );

        return new CoseVerifiedMessage
        {
            MessageType = CoseMessageType.Sign1,
            Content = payload,
            ProtectedHeaders = parsed.Body.Protected,
            UnprotectedHeaders = parsed.Body.Unprotected,
            Algorithm = binding.Algorithm,
            Key = key,
        };
    }

    private CoseVerifiedMessage VerifySign(
        ParsedCoseMessage parsed,
        CoseVerificationParameters parameters
    )
    {
        var payload = ResolveContent(parsed, parameters.DetachedContent);
        CoseHeaderPolicy.EnsureCriticalUnderstood(
            parsed.Body.Protected,
            parameters.UnderstoodCriticalHeaders
        );

        var sawNotPermitted = false;
        var sawEligible = false;
        var sawCandidate = false;
        foreach (var signature in parsed.Signatures)
        {
            CoseHeaderPolicy.EnsureCriticalUnderstood(
                signature.Protected,
                parameters.UnderstoodCriticalHeaders
            );

            CoseAlgorithmBinding binding;
            try
            {
                binding = CoseHeaderPolicy.ResolveLayerAlgorithm(
                    signature,
                    parameters.ValidAlgorithms,
                    CoseAlgorithmKind.Signature,
                    AlgorithmCatalog
                );
            }
            catch (CoseException exception)
                when (exception.FailureCode
                        is CoseFailureCodes.AlgorithmNotPermitted
                            or CoseFailureCodes.UnsupportedAlgorithm
                )
            {
                sawNotPermitted |= exception.FailureCode == CoseFailureCodes.AlgorithmNotPermitted;
                continue;
            }

            sawEligible = true;
            var structure = CoseStructures.Signature(
                parsed.Body.EffectiveProtected.Span,
                signature.EffectiveProtected.Span,
                parameters.ExternalAad.Span,
                payload.Span
            );
            var candidates = SelectKeys(signature, binding, parameters, SecretKeyUses.Signature);
            sawCandidate |= candidates.Count > 0;
            if (FindVerifyingKey(binding, candidates, structure, signature.Third.Span) is { } key)
                return new CoseVerifiedMessage
                {
                    MessageType = CoseMessageType.Sign,
                    Content = payload,
                    ProtectedHeaders = parsed.Body.Protected,
                    UnprotectedHeaders = parsed.Body.Unprotected,
                    Algorithm = binding.Algorithm,
                    Key = key,
                    Signature = new CoseLayerHeaders
                    {
                        ProtectedHeaders = signature.Protected,
                        UnprotectedHeaders = signature.Unprotected,
                    },
                };
        }

        if (sawCandidate)
            throw new CoseException(
                CoseFailureCodes.InvalidSignature,
                "No signature of the message verifies."
            );

        throw new CoseException(
            sawEligible ? CoseFailureCodes.KeyNotFound
                : sawNotPermitted ? CoseFailureCodes.AlgorithmNotPermitted
                : CoseFailureCodes.UnsupportedAlgorithm,
            "No signature of the message can be verified."
        );
    }

    private static ReadOnlyMemory<byte>? CarriedContent(
        ReadOnlySpan<byte> content,
        CoseEncodingOptions options
    )
    {
        if (options.DetachContent)
            return null;

        return content.ToArray();
    }

    private (SignatureAlgorithm Algorithm, CoseAlgorithmBinding Binding) GetSigningAlgorithm(
        CoseCredentials credentials
    )
    {
        if (
            !AlgorithmCatalog.TryGet(credentials.Algorithm.Identifier, out var binding)
            || binding.Algorithm.Kind != CoseAlgorithmKind.Signature
            || binding.JoseAlgorithm is not SignatureAlgorithm algorithm
        )
            throw new ArgumentException(
                "The algorithm is not a registered COSE signature algorithm.",
                nameof(credentials)
            );

        if (!algorithm.IsKeyCompatible(credentials.SecretKey))
            throw new ArgumentException(
                "The key does not suit the signature algorithm.",
                nameof(credentials)
            );

        return (algorithm, binding);
    }

    private static byte[] Sign(SignatureAlgorithm algorithm, SecretKey key, byte[] structure)
    {
        var buffer = new byte[algorithm.GetSignatureSizeBytes(key.KeySizeBits)];
        if (!algorithm.TrySign(key, structure, buffer, out var written))
            throw new InvalidOperationException("The signature does not fit its buffer.");

        return buffer.AsSpan(0, written).ToArray();
    }

    private (
        Dictionary<CoseLabel, CoseValue> Protected,
        Dictionary<CoseLabel, CoseValue> Unprotected
    ) BuildHeaders(
        IReadOnlyDictionary<CoseLabel, CoseValue> callerProtected,
        IReadOnlyDictionary<CoseLabel, CoseValue> callerUnprotected,
        SecretKey? key,
        int? algorithm
    )
    {
        CoseHeaderPolicy.ValidateWrite(callerProtected, callerUnprotected, Options.MaxNestingDepth);

        var protectedHeaders = new Dictionary<CoseLabel, CoseValue>(callerProtected);
        var unprotectedHeaders = new Dictionary<CoseLabel, CoseValue>(callerUnprotected);
        if (algorithm is { } identifier)
            protectedHeaders[CoseHeaderLabels.Algorithm] = CoseValue.FromInt64(identifier);

        if (
            key?.KeyId is { Length: > 0 } keyId
            && !protectedHeaders.ContainsKey(CoseHeaderLabels.KeyId)
            && !unprotectedHeaders.ContainsKey(CoseHeaderLabels.KeyId)
        )
            unprotectedHeaders[CoseHeaderLabels.KeyId] = CoseValue.FromByteString(
                CoseKeyIdentifiers.GetBytes(keyId)
            );

        return (protectedHeaders, unprotectedHeaders);
    }

    private static ReadOnlyMemory<byte> ResolveContent(
        ParsedCoseMessage parsed,
        ReadOnlyMemory<byte>? detached
    )
    {
        if (parsed.Content is { } content)
        {
            return detached is null
                ? content
                : throw new CoseException(
                    CoseFailureCodes.DetachedContent,
                    "The message carries its content and the caller supplied detached content as well."
                );
        }

        return detached
            ?? throw new CoseException(
                CoseFailureCodes.DetachedContent,
                "The content of the message is detached and the caller supplied none."
            );
    }

    private List<SecretKey> SelectKeys(
        ParsedCoseLayer layer,
        CoseAlgorithmBinding binding,
        CoseVerificationParameters parameters,
        string use,
        Func<SecretKey, bool>? extraFilter = null
    ) =>
        SelectKeys(
            layer,
            binding.JoseAlgorithm.Code,
            key =>
                (binding.JoseAlgorithm is not KeyedAlgorithm keyed || keyed.IsKeyCompatible(key))
                && (extraFilter is null || extraFilter(key)),
            parameters.Keys,
            use,
            parameters.ClockSkew
        );

    private List<SecretKey> SelectKeys(
        ParsedCoseLayer layer,
        string algorithmCode,
        Func<SecretKey, bool> isCompatible,
        IEnumerable<SecretKey> keys,
        string use,
        TimeSpan clockSkew
    ) =>
        CoseKeyCandidates.Select(
            keys,
            layer.KeyId,
            algorithmCode,
            isCompatible,
            use,
            TimeProvider.GetUtcNow(),
            clockSkew
        );

    private static SecretKey? FindVerifyingKey(
        CoseAlgorithmBinding binding,
        List<SecretKey> candidates,
        byte[] structure,
        ReadOnlySpan<byte> signature
    )
    {
        var algorithm = (SignatureAlgorithm)binding.JoseAlgorithm;
        foreach (var key in candidates)
        {
            try
            {
                if (algorithm.Verify(key, structure, signature))
                    return key;
            }
            catch (Exception exception)
                when (exception is ArgumentException or CryptographicException)
            {
                // A key that cannot verify is a key that did not verify.
            }
        }

        return null;
    }
}
