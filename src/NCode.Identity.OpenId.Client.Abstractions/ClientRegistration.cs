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

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Describes how this application is registered as a client of an OpenID provider.
/// </summary>
/// <param name="authority">The issuer URL of the OpenID provider.</param>
/// <param name="clientId">The client identifier issued by the OpenID provider.</param>
/// <param name="authenticationMethod">The client authentication method used at the token endpoint.
/// See <see cref="OpenIdProtocol.ClientAuthenticationMethods"/> for the known values.</param>
[PublicAPI]
public sealed class ClientRegistration(
    string authority,
    string clientId,
    string authenticationMethod
)
{
    /// <summary>
    /// Gets the issuer URL of the OpenID provider.
    /// </summary>
    public string Authority { get; } = ValidateNotEmpty(authority);

    /// <summary>
    /// Gets the client identifier issued by the OpenID provider.
    /// </summary>
    public string ClientId { get; } = ValidateNotEmpty(clientId);

    /// <summary>
    /// Gets the client authentication method used at the token endpoint.
    /// </summary>
    public string AuthenticationMethod { get; } = ValidateNotEmpty(authenticationMethod);

    /// <summary>
    /// Gets the name of the keyed <see cref="NCode.Identity.Secrets.Collections.ISecretKeyCollectionProvider"/>
    /// holding the client's credentials and the private keys that decrypt the ID tokens and logout tokens encrypted
    /// to it.
    /// </summary>
    public string? KeyRing { get; init; }

    private IReadOnlyList<string>? _idTokenSigningAlgorithms;

    /// <summary>
    /// Gets the JOSE <c>alg</c> names that ID tokens of this client may be signed with, which is the
    /// <c>id_token_signed_response_alg</c> the client registered at the provider. <see langword="null"/> or empty
    /// (the default) adds no client restriction, so any registered asymmetric algorithm the provider advertises is
    /// accepted. <c>none</c> and symmetric algorithms are never accepted.
    /// </summary>
    public IReadOnlyList<string>? IdTokenSigningAlgorithms
    {
        get => _idTokenSigningAlgorithms;
        init =>
            _idTokenSigningAlgorithms = ValidateAlgorithms(value, "ID-token signing algorithms");
    }

    private IReadOnlyList<string>? _idTokenKeyManagementAlgorithms;

    /// <summary>
    /// Gets the JWE <c>alg</c> names that ID tokens of this client are encrypted with, which is the
    /// <c>id_token_encrypted_response_alg</c> the client registered at the provider. <see langword="null"/> or empty
    /// (the default) means the client registered no ID-token encryption, so an encrypted ID token or logout token is
    /// rejected. When set, every ID token and logout token must be encrypted with one of these algorithms and is
    /// decrypted with the asymmetric keys of <see cref="KeyRing"/>. Only RSA-OAEP and ECDH-ES algorithms are
    /// accepted, never <c>RSA1_5</c>, <c>dir</c> or a shared-key algorithm.
    /// </summary>
    public IReadOnlyList<string>? IdTokenKeyManagementAlgorithms
    {
        get => _idTokenKeyManagementAlgorithms;
        init =>
            _idTokenKeyManagementAlgorithms = ValidateAlgorithms(
                value,
                "ID-token key management algorithms"
            );
    }

    private IReadOnlyList<string>? _idTokenContentEncryptionAlgorithms;

    /// <summary>
    /// Gets the JWE <c>enc</c> names that ID tokens of this client are encrypted with, which is the
    /// <c>id_token_encrypted_response_enc</c> the client registered at the provider. <see langword="null"/> or empty
    /// means <c>A128CBC-HS256</c>, the registration default. It applies only together with
    /// <see cref="IdTokenKeyManagementAlgorithms"/>; setting it alone fails validation with
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public IReadOnlyList<string>? IdTokenContentEncryptionAlgorithms
    {
        get => _idTokenContentEncryptionAlgorithms;
        init =>
            _idTokenContentEncryptionAlgorithms = ValidateAlgorithms(
                value,
                "ID-token content encryption algorithms"
            );
    }

    /// <summary>
    /// Gets a value indicating whether the client registered <c>backchannel_logout_session_required</c>, so a logout
    /// token without a <c>sid</c> claim is rejected. The default is <see langword="false"/>.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client registered <c>require_pushed_authorization_requests</c> (RFC 9126 §6),
    /// so its authorization requests are always pushed and never sent on the front channel. The default is
    /// <see langword="false"/>.
    /// </summary>
    public bool RequirePushedAuthorizationRequests { get; init; }

    private static string[]? ValidateAlgorithms(IReadOnlyList<string>? value, string description)
    {
        if (value is null)
            return null;

        var copy = new string[value.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < copy.Length; i++)
        {
            var algorithm = value[i];
            if (string.IsNullOrWhiteSpace(algorithm))
                throw new ArgumentException(
                    $"The {description} must not contain a null, empty or whitespace entry.",
                    nameof(value)
                );

            if (
                string.Equals(
                    algorithm,
                    AlgorithmCodes.DigitalSignature.None,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new ArgumentException(
                    $"The {description} must not contain 'none'; an unsecured token is never acceptable.",
                    nameof(value)
                );

            if (!seen.Add(algorithm))
                throw new ArgumentException(
                    $"The {description} must not contain duplicates; '{algorithm}' appears more than once.",
                    nameof(value)
                );

            copy[i] = algorithm;
        }

        return copy;
    }

    private static string ValidateNotEmpty(
        string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))]
            string? paramName = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }
}
