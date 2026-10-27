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

using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticator"/> for the <c>private_key_jwt</c> method, which sends a
/// short-lived JWT client assertion signed with an asymmetric key of the client (RFC 7523 §2.2, OpenID Connect Core §9).
/// </summary>
internal class PrivateKeyJwtClientAuthenticator(
    TimeProvider timeProvider,
    IClientKeyRingProvider keyRingProvider,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    IJwtEncoder jwtEncoder,
    IOptions<OpenIdClientOptions> options
) : IClientAuthenticator
{
    private const string AssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    private const string NoneAlgorithm = "none";

    private static readonly TimeSpan AssertionLifetime = TimeSpan.FromSeconds(60);

    private static readonly string[] DefaultPreferredAlgorithms =
    [
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        AlgorithmCodes.DigitalSignature.EcdsaSha256,
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
        AlgorithmCodes.DigitalSignature.EcdsaSha384,
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
        AlgorithmCodes.DigitalSignature.EcdsaSha512,
        AlgorithmCodes.DigitalSignature.RsaSha256,
        AlgorithmCodes.DigitalSignature.RsaSha384,
        AlgorithmCodes.DigitalSignature.RsaSha512,
    ];

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;
    private OpenIdClientOptions Options { get; } = options.Value;

    /// <inheritdoc />
    public string MethodName => ClientAuthenticationMethods.PrivateKeyJwt;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var registration = context.Registration;
        var clientId = registration.ClientId;
        var keyRingName = registration.KeyRing;
        if (string.IsNullOrEmpty(keyRingName))
            throw new InvalidOperationException(
                $"The client '{clientId}' has no key ring for the '{MethodName}' method."
            );

        var audience =
            context.Metadata.Issuer
            ?? throw new InvalidOperationException(
                $"The provider metadata has no issuer to use as the audience of the assertion of the client '{clientId}'."
            );

        var candidateKeys = KeyRingProvider
            .GetKeyRing(keyRingName)
            .OfType<AsymmetricSecretKey>()
            .Cast<SecretKey>()
            .ToList();

        if (
            !CredentialSelector.TryGetSigningCredentials(
                AlgorithmCollectionProvider.Collection,
                BuildPreferredAlgorithms(
                    context.Metadata.TokenEndpointAuthSigningAlgValuesSupported
                ),
                candidateKeys,
                out var credentials
            )
        )
            throw new InvalidOperationException(
                $"The key ring '{keyRingName}' of the client '{clientId}' has no usable asymmetric signing key and algorithm."
            );

        var now = TimeProvider.GetUtcNow();
        var assertion = JwtEncoder.Encode(
            new JwtEncodingParameters
            {
                SigningCredentials = credentials,
                Issuer = clientId,
                Audience = new StringValues(audience),
                IssuedAt = now,
                NotBefore = now,
                Expires = now + AssertionLifetime,
                ExtraPayloadClaims = new Dictionary<string, object>
                {
                    ["sub"] = clientId,
                    ["jti"] = Base64Url.Encode(RandomNumberGenerator.GetBytes(16)),
                },
            }
        );

        context.Parameters.Add(
            new KeyValuePair<string, string>("client_assertion_type", AssertionType)
        );
        context.Parameters.Add(new KeyValuePair<string, string>("client_assertion", assertion));
        context.Parameters.Add(new KeyValuePair<string, string>("client_id", clientId));
        return ValueTask.CompletedTask;
    }

    private List<string> BuildPreferredAlgorithms(List<string>? supported)
    {
        IReadOnlyList<string> preferred = Options.ClientAssertionSigningAlgorithms
            is { Count: > 0 } configured
            ? configured
            : DefaultPreferredAlgorithms;

        if (supported is null || supported.Count == 0)
            return [.. preferred];

        var result = new List<string>();
        foreach (var algorithm in preferred)
        {
            if (supported.Contains(algorithm, StringComparer.Ordinal))
                result.Add(algorithm);
        }

        foreach (var algorithm in supported)
        {
            if (
                !string.Equals(algorithm, NoneAlgorithm, StringComparison.Ordinal)
                && !result.Contains(algorithm, StringComparer.Ordinal)
            )
                result.Add(algorithm);
        }

        return result;
    }
}
