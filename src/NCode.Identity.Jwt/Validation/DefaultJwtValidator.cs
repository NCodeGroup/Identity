#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Json;
using NCode.PropertyBag;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides a default implementation for the <see cref="IJwtValidator"/> interface. Requirements on the
/// <see cref="JwtValidationParameters"/> are evaluated by the registered <see cref="IJwtValidationHandler"/> strategies,
/// which vote on a shared <see cref="JwtValidationContext"/>; the token is valid only when every requirement succeeds
/// and nothing fails.
/// </summary>
/// <remarks>
/// <para>
/// Only a signed JWT is accepted: a compact JWS, or a compact JWE whose <c>cty</c> is <c>JWT</c> enclosing a compact JWS
/// (a Nested JWT, <c>RFC 7519</c> §5.2). An unsecured JWS (<c>alg</c> <c>none</c>) is rejected because it proves nothing
/// about who produced it (<c>RFC 8725</c> §3.2), and so is a JWE that encloses no signature, because anyone holding the
/// recipient's public key can produce one. One level of nesting is supported: the enclosed token must be a JWS. An
/// encrypted token is accepted only when the parameters name its decryption keys, which are never inferred from the
/// keys that verify signatures.
/// </para>
/// <para>
/// The algorithms named by each header are checked against the parameters' algorithm lists before any cryptographic
/// operation, and a key is used only with the use and algorithm its metadata declares (<c>RFC 8725</c> §3.1). Both the
/// enclosing JWE and the enclosed JWS of a Nested JWT are validated (<c>RFC 8725</c> §3.3).
/// </para>
/// </remarks>
/// <param name="serviceProvider">An <see cref="IServiceProvider"/> that can be used to resolve services.</param>
/// <param name="timeProvider">A <see cref="TimeProvider"/> that can be used to get the current time.</param>
/// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
/// <param name="secretKeyCollectionProvider">An <see cref="ISecretKeyCollectionProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
/// <param name="handlers">The registered <see cref="IJwtValidationHandler"/> strategies.</param>
/// <param name="claimsIdentityFactory">The <see cref="IClaimsIdentityFactory"/> that maps a payload to claims.</param>
internal sealed class DefaultJwtValidator(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IJoseSerializer joseSerializer,
    ISecretKeyCollectionProvider secretKeyCollectionProvider,
    IEnumerable<IJwtValidationHandler> handlers,
    IClaimsIdentityFactory claimsIdentityFactory
) : IJwtValidator
{
    private ValueTask<ClaimsIdentity> DefaultCreateClaimsIdentity(
        DecodedJwt decodedJwt,
        IPropertyBag propertyBag,
        string authenticationType,
        string nameClaimType,
        string roleClaimType,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(
            ClaimsIdentityFactory.CreateClaimsIdentity(
                decodedJwt.Payload,
                authenticationType,
                nameClaimType,
                roleClaimType
            )
        );

    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private ISecretKeyCollectionProvider SecretKeyCollectionProvider { get; } =
        secretKeyCollectionProvider;
    private IReadOnlyCollection<IJwtValidationHandler> Handlers { get; } = [.. handlers];
    private IClaimsIdentityFactory ClaimsIdentityFactory { get; } = claimsIdentityFactory;

    /// <inheritdoc />
    public async ValueTask<JwtValidationResult> ValidateAsync(
        string token,
        JwtValidationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        var propertyBag = parameters.PropertyBag.Clone();
        try
        {
            var joseToken = ParseCompact(token);

            var decodedJwt =
                joseToken.ProtectionType == JoseProtectionTypes.Jwe
                    ? await DecodeNestedAsync(joseToken, parameters, propertyBag, cancellationToken)
                    : await DecodeSignedAsync(
                        joseToken,
                        parameters,
                        propertyBag,
                        cancellationToken
                    );

            var context = new JwtValidationContext(
                decodedJwt.SigningKey,
                decodedJwt,
                parameters.Requirements,
                propertyBag,
                ServiceProvider,
                TimeProvider
            );

            foreach (var handler in Handlers)
            {
                await handler.HandleAsync(context, cancellationToken);
            }

            if (!context.HasSucceeded)
            {
                return JwtValidationResult.Fail(
                    parameters,
                    propertyBag,
                    token,
                    new JwtValidationException(BuildFailureReason(context))
                    {
                        FailureCode = context.FailureCode ?? JwtValidationFailureCodes.InvalidToken,
                    }
                );
            }

            var createClaimsIdentity =
                parameters.CreateClaimsIdentityAsync ?? DefaultCreateClaimsIdentity;

            return JwtValidationResult.Success(
                parameters,
                propertyBag,
                decodedJwt,
                createClaimsIdentity
            );
        }
        catch (Exception exception)
        {
            return JwtValidationResult.Fail(parameters, propertyBag, token, exception);
        }
    }

    private JoseToken ParseCompact(string token)
    {
        var joseToken = JoseSerializer.Parse(token);

        // RFC 7519 §1: a JWT is always represented using the JWS or JWE Compact Serialization. The JSON serializations
        // can carry several signatures or recipients, each with its own algorithm, which a single header check cannot
        // vouch for.
        if (joseToken.SerializationFormat != JoseSerializationFormat.Compact)
            throw new JwtValidationException("A JWT must use the compact serialization.");

        return joseToken;
    }

    private async ValueTask<DecodedJwt> DecodeNestedAsync(
        JoseToken encryptedToken,
        JwtValidationParameters parameters,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var header = encryptedToken.Header;

        var keyManagementAlgorithm = EnsurePermitted(
            header,
            JoseClaimNames.Header.Alg,
            parameters.ValidKeyManagementAlgorithms,
            "key management"
        );

        EnsurePermitted(
            header,
            JoseClaimNames.Header.Enc,
            parameters.ValidContentEncryptionAlgorithms,
            "content encryption"
        );

        if (header.TryGetProperty(JoseClaimNames.Header.Zip, out _))
        {
            EnsurePermitted(
                header,
                JoseClaimNames.Header.Zip,
                parameters.ValidCompressionAlgorithms,
                "compression"
            );
        }

        if (
            !header.TryGetPropertyValue<string>(JoseClaimNames.Header.Cty, out var contentType)
            || !JoseTokenTypes.AreSame(contentType, JoseTokenTypes.Jwt)
        )
        {
            throw new JwtValidationException(
                "An encrypted JWT must be a Nested JWT whose 'cty' is 'JWT', enclosing a signed JWT."
            );
        }

        // The keys that decrypt a token and the keys that verify its signature belong to different parties (the
        // recipient and the signer), so an encrypted token is accepted only when the caller names its decryption keys.
        if (parameters.DecryptionKeys is not { } decryptionKeys)
        {
            throw new JwtValidationException(
                "An encrypted JWT is not accepted because no decryption keys were provided."
            );
        }

        var enclosedToken = Decrypt(
            encryptedToken,
            FilterKeys(decryptionKeys, SecretKeyUses.Encryption, keyManagementAlgorithm),
            out var decryptionKey
        );

        var signedToken = ParseCompact(enclosedToken);
        if (signedToken.ProtectionType != JoseProtectionTypes.Jws)
        {
            throw new JwtValidationException("A Nested JWT must enclose a signed JWT (a JWS).");
        }

        var decodedJwt = await DecodeSignedAsync(
            signedToken,
            parameters,
            propertyBag,
            cancellationToken
        );

        return new DecodedJwt(signedToken, decodedJwt.Payload, decodedJwt.SigningKey)
        {
            EncryptionHeader = header,
            DecryptionKey = decryptionKey,
        };
    }

    private async ValueTask<DecodedJwt> DecodeSignedAsync(
        JoseToken signedToken,
        JwtValidationParameters parameters,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var header = signedToken.Header;

        var signatureAlgorithm = EnsurePermitted(
            header,
            JoseClaimNames.Header.Alg,
            parameters.ValidSignatureAlgorithms,
            "signature"
        );

        // RFC 8725 §3.2: an unsecured JWS proves nothing about who produced it, so it is never consumed; no parameter
        // opts into it.
        if (
            string.Equals(
                signatureAlgorithm,
                AlgorithmCodes.DigitalSignature.None,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new JwtValidationException(
                "An unsecured JWT ('alg' of 'none') is never accepted."
            )
            {
                FailureCode = JwtValidationFailureCodes.AlgorithmNotPermitted,
            };
        }

        var validationKeys = await ResolveValidationKeysAsync(
            signedToken,
            parameters,
            propertyBag,
            cancellationToken
        );

        var payload = DeserializePayload(
            signedToken,
            FilterKeys(validationKeys, SecretKeyUses.Signature, signatureAlgorithm),
            out var secretKey
        );

        return new DecodedJwt(signedToken, payload, secretKey);
    }

    // RFC 8725 §3.1: the algorithm a header names must be one the caller permits, and it is checked before the token is
    // used in any cryptographic operation.
    private static string EnsurePermitted(
        JsonElement header,
        string headerName,
        IReadOnlyCollection<string>? permittedAlgorithms,
        string algorithmKind
    )
    {
        if (
            !header.TryGetPropertyValue<string>(headerName, out var algorithm)
            || string.IsNullOrEmpty(algorithm)
        )
        {
            throw new JwtValidationException(
                $"The JWT header is missing the '{headerName}' parameter."
            );
        }

        if (
            permittedAlgorithms is not null
            && !permittedAlgorithms.Contains(algorithm, StringComparer.Ordinal)
        )
        {
            throw new JwtValidationException(
                $"The {algorithmKind} algorithm '{algorithm}' is not permitted."
            )
            {
                FailureCode = JwtValidationFailureCodes.AlgorithmNotPermitted,
            };
        }

        return algorithm;
    }

    // RFC 8725 §3.1: a key is used only for the use and with the algorithm its metadata declares; a key that declares
    // neither may serve any.
    private static IEnumerable<SecretKey> FilterKeys(
        IEnumerable<SecretKey> secretKeys,
        string use,
        string algorithm
    ) =>
        secretKeys.Where(secretKey =>
            (secretKey.Metadata.Use is null || secretKey.Metadata.Use == use)
            && (secretKey.Metadata.Algorithm is null || secretKey.Metadata.Algorithm == algorithm)
        );

    private async ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
        JoseToken joseToken,
        JwtValidationParameters parameters,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (parameters.ResolveValidationKeysAsync is { } resolve)
        {
            return await resolve(
                joseToken,
                propertyBag,
                SecretKeyCollectionProvider,
                cancellationToken
            );
        }

        if (parameters.ValidationKeys is { } explicitKeys)
        {
            return explicitKeys;
        }

        return DefaultValidationKeyResolver.ResolveValidationKeys(
            joseToken.ProtectionType,
            joseToken.Header,
            SecretKeyCollectionProvider.Collection
        );
    }

    private static string BuildFailureReason(JwtValidationContext context)
    {
        if (context.FailureReasons.Count > 0)
            return string.Join(" ", context.FailureReasons);

        return "One or more token validation requirements were not satisfied.";
    }

    private string Decrypt(
        JoseToken encryptedToken,
        IEnumerable<SecretKey> decryptionKeys,
        out SecretKey secretKey
    ) =>
        TryEachKey(
            encryptedToken,
            decryptionKeys,
            JwtValidationFailureCodes.DecryptionFailed,
            (token, key) => JoseSerializer.Decode(token, key),
            out secretKey
        );

    private JsonElement DeserializePayload(
        JoseToken signedToken,
        IEnumerable<SecretKey> validationKeys,
        out SecretKey secretKey
    ) =>
        TryEachKey(
            signedToken,
            validationKeys,
            JwtValidationFailureCodes.InvalidSignature,
            (token, key) => JoseSerializer.Deserialize<JsonElement>(token, key),
            out secretKey
        );

    private static T TryEachKey<T>(
        JoseToken joseToken,
        IEnumerable<SecretKey> candidateKeys,
        string decodeFailureCode,
        Func<JoseToken, SecretKey, T> decode,
        out SecretKey secretKey
    )
    {
        StringBuilder? exceptions = null;
        StringBuilder? attemptedKeys = null;

        foreach (var attemptedKey in candidateKeys)
        {
            try
            {
                var result = decode(joseToken, attemptedKey);

                secretKey = attemptedKey;
                return result;
            }
            catch (Exception exception)
            {
                exceptions ??= new StringBuilder("Inner exceptions:\n");
                exceptions.Append(exception);
                exceptions.Append('\n');
            }

            attemptedKeys ??= new StringBuilder("Keys attempted:\n");
            attemptedKeys.Append(attemptedKey);
            attemptedKeys.Append('\n');
        }

        if (attemptedKeys is null)
        {
            throw new JwtSecretKeyNotFoundException();
        }

        Debug.Assert(exceptions != null);

        throw new JwtDecodeException(
            $"{JwtDecodeException.DefaultMessage}.\n\nToken:\n{joseToken}\n\nKeys attempted:\n{attemptedKeys}\nInner exceptions:\n{exceptions}"
        )
        {
            FailureCode = decodeFailureCode,
        };
    }
}
