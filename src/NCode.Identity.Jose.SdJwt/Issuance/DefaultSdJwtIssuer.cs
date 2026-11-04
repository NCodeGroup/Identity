// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtIssuer"/>. The claims are walked depth first, so a
/// disclosable claim inside another disclosable claim is replaced by its digest before the outer claim is moved into
/// its own disclosure, which is the recursive structure of RFC 9901 §5.2.4. Each <c>_sd</c> array is sorted so the
/// order of its digests reveals nothing about the claims, and the issuer-signed JWT is encoded by the JOSE serializer.
/// </summary>
/// <param name="joseSerializer">The JOSE serializer that signs the issuer-signed JWT.</param>
/// <param name="digestAlgorithmCatalog">The registered hash algorithms.</param>
/// <param name="randomSource">The source of salts and decoy positions.</param>
/// <param name="optionsAccessor">The accessor of the limits.</param>
// See ADR-1405.
internal class DefaultSdJwtIssuer(
    IJoseSerializer joseSerializer,
    ISdJwtDigestAlgorithmCatalog digestAlgorithmCatalog,
    ISdJwtRandomSource randomSource,
    IOptions<SdJwtOptions> optionsAccessor
) : ISdJwtIssuer
{
    private const string RootPath = "";

    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private ISdJwtDigestAlgorithmCatalog DigestAlgorithmCatalog { get; } = digestAlgorithmCatalog;
    private ISdJwtRandomSource RandomSource { get; } = randomSource;
    private SdJwtOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public SelectiveDisclosureJwt Issue(SdJwtIssuanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var signingCredentials = options.SigningCredentials;
        if (signingCredentials.SecretKey is not AsymmetricSecretKey)
            throw new ArgumentException(
                "The issuer-signed JWT must be signed with an asymmetric key.",
                nameof(options)
            );

        if (!DigestAlgorithmCatalog.TryGet(options.DigestAlgorithm, out var algorithm))
            throw new ArgumentException(
                $"The digest algorithm '{options.DigestAlgorithm}' is not registered.",
                nameof(options)
            );

        if (options.DecoyDigestCount < 0)
            throw new ArgumentException(
                "The decoy digest count must not be negative.",
                nameof(options)
            );

        var disclosablePaths = ReadDisclosablePaths(options);
        var claims = ReadClaims(options);

        var holderKey = options.HolderKey;
        if (holderKey is not null)
        {
            if (holderKey.ContainsPrivateKeyMaterial)
                throw new ArgumentException(
                    "The holder key must carry only public members.",
                    nameof(options)
                );

            if (claims.ContainsKey(JoseClaimNames.Payload.Cnf))
                throw new ArgumentException(
                    "The claims must not carry 'cnf' when a holder key is given.",
                    nameof(options)
                );
        }

        var walk = new IssuanceWalk(
            algorithm,
            RandomSource,
            disclosablePaths,
            options.DecoyDigestCount
        );
        walk.ProcessObject(claims, RootPath);

        if (disclosablePaths.Count > 0)
            throw new ArgumentException(
                $"The disclosable path '{disclosablePaths.First()}' names no claim.",
                nameof(options)
            );

        if (walk.WroteDigests)
            claims[JoseClaimNames.Payload.SdAlg] = algorithm.Name;

        if (holderKey is not null)
            claims[JoseClaimNames.Payload.Cnf] = new JsonObject
            {
                [JoseClaimNames.Header.Jwk] = JsonObject.Create(holderKey.AsElement()),
            };

        var signature = new JoseSignatureOptions(signingCredentials)
        {
            TokenType = options.Profile?.TokenType ?? JoseTokenTypes.Jwt,
        };
        var encoder = JoseSerializer.CreateEncoder(new JoseSigningOptions(signature));
        var issuerSignedJwt = encoder.Encode<JsonNode>(claims);

        return new SelectiveDisclosureJwt(issuerSignedJwt, walk.Disclosures, keyBindingJwt: null);
    }

    private HashSet<string> ReadDisclosablePaths(SdJwtIssuanceOptions options)
    {
        var paths = options.DisclosablePaths;
        if (paths.Count > Options.MaxDisclosureCount)
            throw new ArgumentException(
                $"The SD-JWT would carry {paths.Count} disclosures, more than the limit of {Options.MaxDisclosureCount}.",
                nameof(options)
            );

        var disclosablePaths = new HashSet<string>(paths.Count, StringComparer.Ordinal);
        var protectedClaims = options.Profile?.ProtectedClaims;
        foreach (var path in paths)
        {
            if (path is null || !JsonPointer.IsWellFormed(path))
                throw new ArgumentException(
                    $"The disclosable path '{path}' is not a JSON Pointer to a member or element.",
                    nameof(options)
                );

            if (!disclosablePaths.Add(path))
                throw new ArgumentException(
                    $"The disclosable path '{path}' is listed more than once.",
                    nameof(options)
                );

            if (
                protectedClaims is not null
                && protectedClaims.Contains(JsonPointer.GetFirstToken(path))
            )
                throw new ArgumentException(
                    $"The disclosable path '{path}' names a claim that is never selectively disclosable under the '{options.Profile?.Name}' profile.",
                    nameof(options)
                );
        }

        return disclosablePaths;
    }

    private static JsonObject ReadClaims(SdJwtIssuanceOptions options)
    {
        var element = options.Claims;
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The claims must be a JSON object.", nameof(options));

        JsonObject claims;
        try
        {
            claims =
                JsonObject.Create(element)
                ?? throw new ArgumentException(
                    "The claims must be a JSON object.",
                    nameof(options)
                );
            if (FindReservedName(claims) is { } reservedName)
                throw new ArgumentException(
                    $"The claims must not use the reserved name '{reservedName}'.",
                    nameof(options)
                );
        }
        catch (ArgumentException exception)
            when (!string.Equals(exception.ParamName, nameof(options), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The claims repeat a member name.",
                nameof(options),
                exception
            );
        }

        if (options.Profile is { } profile)
        {
            foreach (var requiredClaim in profile.RequiredClaims)
            {
                if (
                    !element.TryGetPropertyValue<string>(requiredClaim, out var value)
                    || string.IsNullOrEmpty(value)
                )
                    throw new ArgumentException(
                        $"The '{profile.Name}' profile requires the '{requiredClaim}' claim as a non-empty string.",
                        nameof(options)
                    );
            }
        }

        return claims;
    }

    // The first reserved member name found at any depth, or null when the claims use none.
    private static string? FindReservedName(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var member in obj)
                {
                    if (SdJwtClaimRules.IsReserved(member.Key))
                        return member.Key;

                    if (FindReservedName(member.Value) is { } nested)
                        return nested;
                }
                break;

            case JsonArray array:
                foreach (var element in array)
                {
                    if (FindReservedName(element) is { } nested)
                        return nested;
                }
                break;
        }

        return null;
    }
}
