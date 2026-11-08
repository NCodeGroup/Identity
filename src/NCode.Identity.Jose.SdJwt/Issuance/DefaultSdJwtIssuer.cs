// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Json;

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtIssuer"/>. The claims are walked depth first, so a
/// disclosable claim inside another disclosable claim is replaced by its digest before the outer claim is moved into
/// its own disclosure, which is the recursive structure of RFC 9901 §5.2.4. Each <c>_sd</c> array is sorted so the
/// order of its digests reveals nothing about the claims, and the issuer-signed JWT is encoded by the JOSE serializer.
/// A protected top-level claim, by the format's own rule or the profile's, is never made disclosable in whole or in
/// part, and the claims may nest no deeper than the limit the verifier enforces on the resolved claims, so the
/// verifier accepts every SD-JWT the issuer makes.
/// </summary>
/// <param name="joseSerializer">The JOSE serializer that signs the issuer-signed JWT.</param>
/// <param name="digestAlgorithmCatalog">The registered hash algorithms.</param>
/// <param name="secretKeyTypeCatalog">The registered secret key types, whose JWK rules screen the holder key.</param>
/// <param name="randomSource">The source of salts and decoy positions.</param>
/// <param name="optionsAccessor">The accessor of the limits.</param>
// See ADR-1405.
internal class DefaultSdJwtIssuer(
    IJoseSerializer joseSerializer,
    ISdJwtDigestAlgorithmCatalog digestAlgorithmCatalog,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISdJwtRandomSource randomSource,
    IOptions<SdJwtOptions> optionsAccessor
) : ISdJwtIssuer
{
    private const string RootPath = "";

    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private ISdJwtDigestAlgorithmCatalog DigestAlgorithmCatalog { get; } = digestAlgorithmCatalog;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private ISdJwtRandomSource RandomSource { get; } = randomSource;
    private SdJwtOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public async ValueTask<SelectiveDisclosureJwt> IssueAsync(
        SdJwtIssuanceOptions options,
        CancellationToken cancellationToken
    )
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
            EnsurePublicHolderKey(options, holderKey);

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
        var issuerSignedJwt = await encoder.EncodeAsync<JsonNode>(claims, cancellationToken);

        return new SelectiveDisclosureJwt(issuerSignedJwt, walk.Disclosures, keyBindingJwt: null);
    }

    // The key type's own private members count too, not only the standard private names, so the key is read under
    // its registered descriptor; a key of an unregistered or symmetric type binds no presentation.
    private void EnsurePublicHolderKey(SdJwtIssuanceOptions options, JsonWebKey holderKey)
    {
        if (
            !SecretKeyTypeCatalog.TryGetByJwkKeyType(holderKey.KeyType, out var descriptor)
            || descriptor.JwkKeyType is not { } jwkKeyType
        )
            throw new ArgumentException(
                $"The holder key type '{holderKey.KeyType}' is not registered.",
                nameof(options)
            );

        if (!descriptor.IsAsymmetric)
            throw new ArgumentException("The holder key must be asymmetric.", nameof(options));

        if (new JsonWebKey(holderKey.AsElement(), jwkKeyType).ContainsPrivateKeyMaterial)
            throw new ArgumentException(
                "The holder key must carry only public members.",
                nameof(options)
            );
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
        var profile = options.Profile;
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

            var claimName = JsonPointer.GetFirstToken(path);
            if (SdJwtClaimRules.IsProtected(claimName, profile))
                throw new ArgumentException(
                    $"The disclosable path '{path}' names a protected claim. {SdJwtClaimRules.DescribeProtected(claimName, profile)}",
                    nameof(options)
                );
        }

        return disclosablePaths;
    }

    private JsonObject ReadClaims(SdJwtIssuanceOptions options)
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
            ValidateShape(claims, depth: 0, nameof(options));
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

    // A reserved member name at any depth is refused, and so are claims that nest deeper than the limit the payload
    // processor enforces on the resolved claims, counted the same way: the root object is at depth zero and each
    // nested object or array one deeper.
    private void ValidateShape(JsonNode? node, int depth, string paramName)
    {
        switch (node)
        {
            case JsonObject obj:
                EnsureDepth(depth, paramName);
                foreach (var member in obj)
                {
                    if (SdJwtClaimRules.IsReserved(member.Key))
                        throw new ArgumentException(
                            $"The claims must not use the reserved name '{member.Key}'.",
                            paramName
                        );

                    ValidateShape(member.Value, depth + 1, paramName);
                }
                break;

            case JsonArray array:
                EnsureDepth(depth, paramName);
                foreach (var element in array)
                    ValidateShape(element, depth + 1, paramName);
                break;
        }
    }

    private void EnsureDepth(int depth, string paramName)
    {
        if (depth > Options.MaxNestingDepth)
            throw new ArgumentException(
                $"The claims nest deeper than the limit of {Options.MaxNestingDepth}.",
                paramName
            );
    }
}
