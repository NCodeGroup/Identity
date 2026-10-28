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

using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientJwtCredentialSelector"/>. A JWT is signed with one of the
/// tenant's keys and encrypted to one of the client's public keys — its registered <c>jwks</c> or the keys its
/// <c>jwks_uri</c> publishes — never to a shared key, which the client's registration does not establish.
/// </summary>
// See ADR-0155.
internal class DefaultClientJwtCredentialSelector(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    IClientKeyResolver clientKeyResolver
) : IClientJwtCredentialSelector
{
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IClientKeyResolver ClientKeyResolver { get; } = clientKeyResolver;

    /// <inheritdoc />
    public async ValueTask<ClientJwtCredentials> SelectCredentialsAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ClientJwtProtectionSettings protectionSettings,
        CancellationToken cancellationToken
    )
    {
        var signingCredentials = SelectSigningCredentials(
            openIdContext,
            openIdClient,
            protectionSettings
        );

        var encryptionCredentials = await SelectEncryptionCredentialsAsync(
            openIdContext,
            openIdClient,
            protectionSettings,
            cancellationToken
        );

        return new ClientJwtCredentials(signingCredentials, encryptionCredentials);
    }

    private JoseSigningCredentials? SelectSigningCredentials(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ClientJwtProtectionSettings protectionSettings
    )
    {
        var settings = openIdClient.Settings;

        if (
            !settings.TryGetValue(protectionSettings.SignedResponseAlg, out var algorithm)
            || string.IsNullOrEmpty(algorithm)
        )
        {
            return null;
        }

        EnsurePermitted(
            openIdContext,
            settings,
            protectionSettings.SignedResponseAlg,
            algorithm,
            protectionSettings.SigningAlgValuesSupported
        );

        var tenantKeys = openIdContext.Tenant.SecretsProvider.Collection;
        if (
            !CredentialSelector.TryGetSigningCredentials(
                AlgorithmCollectionProvider.Collection,
                [algorithm],
                tenantKeys,
                out var signingCredentials
            )
        )
        {
            throw CreateServerError(
                openIdContext,
                $"The tenant has no key to sign the client's {protectionSettings.ResponseName} with '{algorithm}'."
            );
        }

        return signingCredentials;
    }

    private async ValueTask<JoseEncryptionCredentials?> SelectEncryptionCredentialsAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ClientJwtProtectionSettings protectionSettings,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;

        if (
            !settings.TryGetValue(
                protectionSettings.EncryptedResponseAlg,
                out var keyManagementAlgorithm
            ) || string.IsNullOrEmpty(keyManagementAlgorithm)
        )
        {
            // A policy that requires encryption never guesses an algorithm the client did not register.
            if (IsEncryptionRequired(settings, protectionSettings))
            {
                throw CreateServerError(
                    openIdContext,
                    $"The client's {protectionSettings.ResponseName} must be encrypted, but the client registers no '{protectionSettings.EncryptedResponseAlg.SettingName}'."
                );
            }

            return null;
        }

        var contentEncryptionAlgorithm =
            settings.TryGetValue(protectionSettings.EncryptedResponseEnc, out var registeredEnc)
            && !string.IsNullOrEmpty(registeredEnc)
                ? registeredEnc
                : AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256;

        EnsurePermitted(
            openIdContext,
            settings,
            protectionSettings.EncryptedResponseAlg,
            keyManagementAlgorithm,
            protectionSettings.EncryptionAlgValuesSupported
        );

        EnsurePermitted(
            openIdContext,
            settings,
            protectionSettings.EncryptedResponseEnc,
            contentEncryptionAlgorithm,
            protectionSettings.EncryptionEncValuesSupported
        );

        var clientKeys = await ClientKeyResolver.ResolveSecretKeysAsync(
            openIdClient,
            cancellationToken
        );

        // Only a public key the client published can receive an encrypted response; a shared secret the client
        // authenticates with is not an encryption key the client registered. The server never compresses what it
        // encrypts (RFC 8725 §3.6).
        List<SecretKey> publicKeys = [.. clientKeys.OfType<AsymmetricSecretKey>()];
        if (
            !CredentialSelector.TryGetEncryptionCredentials(
                AlgorithmCollectionProvider.Collection,
                [keyManagementAlgorithm],
                [contentEncryptionAlgorithm],
                [],
                publicKeys,
                out var encryptionCredentials
            )
        )
        {
            throw CreateServerError(
                openIdContext,
                $"The client has no public key to encrypt its {protectionSettings.ResponseName} to with '{keyManagementAlgorithm}'."
            );
        }

        return encryptionCredentials;
    }

    private static bool IsEncryptionRequired(
        IReadOnlySettingCollection settings,
        ClientJwtProtectionSettings protectionSettings
    ) =>
        protectionSettings.EncryptionRequired is { } encryptionRequired
        && settings.TryGetValue(encryptionRequired, out var required)
        && required;

    // A registered algorithm binds only within the client's effective list, which carries the tenant's list and the
    // ceiling of every security profile the client selects; an unset list does not restrict it.
    private static void EnsurePermitted(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings,
        SettingKey<string> registeredKey,
        string algorithm,
        SettingKey<IReadOnlyCollection<string>> permittedKey
    )
    {
        if (
            settings.TryGetValue(permittedKey, out var permitted)
            && !permitted.Contains(algorithm, StringComparer.Ordinal)
        )
        {
            throw CreateServerError(
                openIdContext,
                $"The client's '{registeredKey.SettingName}' value '{algorithm}' is not permitted by its '{permittedKey.SettingName}'."
            );
        }
    }

    private static OpenIdException CreateServerError(
        OpenIdContext openIdContext,
        string description
    ) =>
        openIdContext
            .ErrorFactory.Create(OpenIdProtocol.ErrorCodes.ServerError)
            .WithDescription(description)
            .WithStatusCode(StatusCodes.Status500InternalServerError)
            .AsException();
}
