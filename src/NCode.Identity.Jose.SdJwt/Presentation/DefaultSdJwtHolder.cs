// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Processing;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Secrets.Keys;
using NCode.Json;
using NCode.Time;

namespace NCode.Identity.Jose.SdJwt.Presentation;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtHolder"/>. The presentation is the issuer-signed JWT
/// followed by the chosen disclosures in the order the issuer gave them, and the Key Binding JWT is a compact
/// <c>kb+jwt</c> JWS over <c>iat</c>, <c>aud</c>, <c>nonce</c> and the <c>sd_hash</c> of everything before it,
/// computed with the SD-JWT's own <c>_sd_alg</c> (RFC 9901 §4.3).
/// </summary>
/// <param name="joseSerializer">The JOSE serializer that reads the issuer-signed JWT and signs the Key Binding JWT.</param>
/// <param name="digestAlgorithmCatalog">The registered hash algorithms.</param>
/// <param name="timeProvider">The clock that stamps the Key Binding JWT's <c>iat</c> claim.</param>
/// <param name="optionsAccessor">The accessor of the limits.</param>
// See ADR-1405.
internal class DefaultSdJwtHolder(
    IJoseSerializer joseSerializer,
    ISdJwtDigestAlgorithmCatalog digestAlgorithmCatalog,
    TimeProvider timeProvider,
    IOptions<SdJwtOptions> optionsAccessor
) : ISdJwtHolder
{
    private const int JwsPayloadSegment = 1;

    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private ISdJwtDigestAlgorithmCatalog DigestAlgorithmCatalog { get; } = digestAlgorithmCatalog;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private SdJwtOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public SelectiveDisclosureJwt Parse(string serialized) =>
        SdJwtSerialization.Parse(serialized, Options.MaxDisclosureCount);

    /// <inheritdoc />
    public IReadOnlyList<DisclosedClaim> ResolveDisclosures(SelectiveDisclosureJwt sdJwt)
    {
        ArgumentNullException.ThrowIfNull(sdJwt);

        return Resolve(sdJwt).Processed.Disclosed;
    }

    /// <inheritdoc />
    public string CreatePresentation(SdJwtPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sdJwt = options.SdJwt;
        var keyBinding = options.KeyBinding;
        if (keyBinding is not null)
        {
            if (keyBinding.SigningCredentials.SecretKey is not AsymmetricSecretKey)
                throw new ArgumentException(
                    "The Key Binding JWT must be signed with an asymmetric key.",
                    nameof(options)
                );

            if (string.IsNullOrEmpty(keyBinding.Audience))
                throw new ArgumentException(
                    "The Key Binding JWT's audience must not be empty.",
                    nameof(options)
                );

            if (string.IsNullOrEmpty(keyBinding.Nonce))
                throw new ArgumentException(
                    "The Key Binding JWT's nonce must not be empty.",
                    nameof(options)
                );
        }

        var (algorithm, processed) = Resolve(sdJwt);
        var parentsByDisclosure = new Dictionary<Disclosure, Disclosure?>(
            ReferenceEqualityComparer.Instance
        );
        foreach (var disclosed in processed.Disclosed)
            parentsByDisclosure[disclosed.Disclosure] = disclosed.Parent;

        var selected = new HashSet<Disclosure>(ReferenceEqualityComparer.Instance);
        foreach (var disclosure in options.Disclosures)
        {
            if (!parentsByDisclosure.ContainsKey(disclosure))
                throw new ArgumentException(
                    "A disclosure to reveal is not a disclosure of the SD-JWT.",
                    nameof(options)
                );

            // A nested disclosure is resolved only through the disclosure that carries its digest.
            for (var current = disclosure; current is not null && selected.Add(current); )
                current = parentsByDisclosure[current];
        }

        List<Disclosure> presented = [.. sdJwt.Disclosures.Where(selected.Contains)];
        var presentation = SdJwtSerialization.Serialize(
            sdJwt.IssuerSignedJwt,
            presented,
            keyBindingJwt: null
        );

        if (keyBinding is null)
            return presentation;

        return presentation + CreateKeyBindingJwt(keyBinding, algorithm, presentation);
    }

    private string CreateKeyBindingJwt(
        SdJwtKeyBindingOptions keyBinding,
        SdJwtDigestAlgorithm algorithm,
        string presentation
    )
    {
        var payload = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.Iat] = TimeProvider
                .GetUtcNowWithPrecisionInSeconds()
                .ToUnixTimeSeconds(),
            [JoseClaimNames.Payload.Aud] = keyBinding.Audience,
            [JoseClaimNames.Payload.Nonce] = keyBinding.Nonce,
            [JoseClaimNames.Payload.SdHash] = algorithm.ComputeDigest(presentation),
        };

        var signature = new JoseSignatureOptions(keyBinding.SigningCredentials)
        {
            TokenType = JoseTokenTypes.KeyBindingJwt,
        };
        var encoder = JoseSerializer.CreateEncoder(new JoseSigningOptions(signature));
        return encoder.Encode(payload);
    }

    private (SdJwtDigestAlgorithm Algorithm, ProcessedSdJwtPayload Processed) Resolve(
        SelectiveDisclosureJwt sdJwt
    )
    {
        var payload = ReadPayload(sdJwt.IssuerSignedJwt);
        var algorithm = ResolveDigestAlgorithm(payload);
        var processor = new SdJwtPayloadProcessor(
            algorithm,
            Options.MaxNestingDepth,
            profile: null
        );
        return (algorithm, processor.Process(payload, sdJwt.Disclosures));
    }

    private SdJwtDigestAlgorithm ResolveDigestAlgorithm(JsonElement payload)
    {
        var name = SdJwtDigestAlgorithms.DefaultName;
        if (
            payload.TryGetProperty(JoseClaimNames.Payload.SdAlg, out var sdAlg)
            && (!sdAlg.TryGetValue(out name) || string.IsNullOrEmpty(name))
        )
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm,
                "The '_sd_alg' claim must be a non-empty string."
            );

        return DigestAlgorithmCatalog.TryGet(name, out var algorithm)
            ? algorithm
            : throw new SdJwtException(
                SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm,
                $"The digest algorithm '{name}' is not registered."
            );
    }

    // The holder reads the payload it was issued without verifying the signature; verification is the verifier's.
    private JsonElement ReadPayload(string issuerSignedJwt)
    {
        CompactJwt compactJwt;
        try
        {
            compactJwt =
                JoseSerializer.Parse(issuerSignedJwt) as CompactJwt
                ?? throw new SdJwtException(
                    SdJwtVerificationFailureCodes.InvalidSdJwt,
                    "The issuer-signed JWT must be a compact JWS."
                );
        }
        catch (Exception exception) when (exception is JoseException or JsonException)
        {
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT must be a compact JWS.",
                exception
            );
        }

        if (
            compactJwt.ProtectionType != JoseProtectionTypes.Jws
            || compactJwt.Header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64)
                && !b64
        )
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT must be a compact JWS with a base64url-encoded payload."
            );

        var encodedPayload = compactJwt.Segments[JwsPayloadSegment].Span;
        try
        {
            var byteCount = Base64Url.GetByteCountForDecode(encodedPayload.Length);
            using var lease = BufferFactory.Rent(
                byteCount,
                isSensitive: false,
                out Span<byte> utf8Json
            );
            if (
                !Base64Url.TryDecode(encodedPayload, utf8Json, out var bytesWritten)
                || bytesWritten != byteCount
            )
                throw new FormatException("The issuer-signed JWT payload is not valid base64url.");

            var payload = JsonElement.Parse(utf8Json);
            if (payload.ValueKind != JsonValueKind.Object)
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.InvalidSdJwt,
                    "The issuer-signed JWT payload must be a JSON object."
                );

            return payload;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT payload must be a base64url-encoded JSON object.",
                exception
            );
        }
    }
}
