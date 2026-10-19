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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the default implementation of <see cref="IAccessTokenPolicyService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The settings that govern an access token are the client's effective settings combined with the settings that apply
/// to resource servers of every resource server the token targets, by their lattice merges
/// (<see cref="IReadOnlySettingCollection.Combine"/>): a requirement any of them states holds, and a list permits only
/// what all of them permit, in the server's order. Every audience of a multi-audience token binds it.
/// </para>
/// <para>
/// An encrypted access token is encrypted to the party that reads it. A resource server that holds a key it can decrypt
/// with under its own effective settings decrypts the tokens issued for it, so a token for it alone is encrypted to that
/// key, and a token for it together with another resource server is refused, because a compact JWE has one recipient.
/// Any other token is encrypted to the tenant's key, and its resource servers introspect it. Access tokens are always
/// signed with the tenant's key.
/// </para>
/// </remarks>
// See ADR-0158 (amending ADR-0075).
internal class DefaultAccessTokenPolicyService(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    ISettingScopeCollectionProvider settingScopeCollectionProvider
) : IAccessTokenPolicyService
{
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ICredentialSelector CredentialSelector { get; } = credentialSelector;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;

    /// <inheritdoc />
    public AccessTokenPolicy Evaluate(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        TokenConfirmation confirmation,
        ResourceServerCredentialResolution resolution
    )
    {
        var settings = CombineSettings(openIdClient.Settings, resolution);

        EnsureSenderConstraint(openIdContext, settings, confirmation);

        return new AccessTokenPolicy
        {
            Settings = settings,
            SigningCredentials = SelectSigningCredentials(openIdContext, settings),
            EncryptionCredentials = SelectEncryptionCredentials(
                openIdContext,
                settings,
                resolution
            ),
        };
    }

    // The client is the left operand of every combination, so a list keeps the client's order — the server's, for every
    // list the server's settings set, since Intersect keeps its left operand's order down the whole cascade.
    private IReadOnlySettingCollection CombineSettings(
        IReadOnlySettingCollection clientSettings,
        ResourceServerCredentialResolution resolution
    )
    {
        if (resolution.ResourceServers.Count == 0)
        {
            return clientSettings;
        }

        var scopes = SettingScopeCollectionProvider.Collection;
        var settings = clientSettings;

        foreach (var resourceServer in resolution.ResourceServers)
        {
            var bounds = resourceServer.EffectiveSettings.Where(setting =>
                scopes.AppliesWithin(setting.Descriptor, OpenIdSettingScopes.ResourceServer)
            );

            settings = settings.Combine(bounds);
        }

        return settings;
    }

    // A required sender constraint is met only by a binding whose 'cnf' method every targeted resource server verifies;
    // when none of them names its methods, any binding the server makes meets it.
    private static void EnsureSenderConstraint(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings,
        TokenConfirmation confirmation
    )
    {
        if (!settings.GetValue(OpenIdSettingKeys.AccessTokenSenderConstraintRequired))
        {
            return;
        }

        var boundMethods = confirmation.ToClaimValueOrNull()?.Keys.ToList();
        if (boundMethods is null)
        {
            throw openIdContext
                .ErrorFactory.InvalidRequest(
                    "The access token must be sender-constrained; present a DPoP proof or a bound client certificate."
                )
                .AsException();
        }

        if (
            !settings.TryGetValue(
                OpenIdSettingKeys.AccessTokenConfirmationMethodsSupported,
                out var permittedMethods
            )
            || boundMethods.Any(method => permittedMethods.Contains(method, StringComparer.Ordinal))
        )
        {
            return;
        }

        throw openIdContext
            .ErrorFactory.InvalidRequest(
                $"The access token must be bound by a confirmation method every resource server it targets verifies "
                    + $"({Describe(permittedMethods)}), but it is bound by {Describe(boundMethods)}."
            )
            .AsException();
    }

    // Access tokens are always signed with a tenant key and validated through the tenant's JWKS (RFC 9068); a resource
    // server never holds a token-signing key (ADR-0075).
    private JoseSigningCredentials SelectSigningCredentials(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings
    )
    {
        var permitted = settings.GetPermittedAlgorithms(
            OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported
        );

        if (
            !CredentialSelector.TryGetSigningCredentials(
                AlgorithmCollectionProvider.Collection,
                permitted,
                openIdContext.Tenant.SecretsProvider.Collection,
                out var signingCredentials
            )
        )
        {
            throw CreateServerError(
                openIdContext,
                "The tenant has no key to sign the access token with an algorithm its "
                    + $"'{OpenIdSettingNames.AccessTokenSigningAlgValuesSupported}' permits ({Describe(permitted)})."
            );
        }

        return signingCredentials;
    }

    private JoseEncryptionCredentials? SelectEncryptionCredentials(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings,
        ResourceServerCredentialResolution resolution
    )
    {
        if (!settings.GetValue(OpenIdSettingKeys.AccessTokenEncryptionRequired))
        {
            return null;
        }

        var reader = resolution.ResourceServers.FirstOrDefault(DecryptsItsOwnTokens);
        if (reader is null)
        {
            return SelectEncryptionCredentials(
                openIdContext,
                settings,
                openIdContext.Tenant.SecretsProvider.Collection,
                "The tenant has no key to encrypt the access token to with an algorithm its encryption settings permit."
            );
        }

        if (resolution.ResourceServers.Count > 1)
        {
            throw openIdContext
                .ErrorFactory.InvalidRequest(
                    $"The access token must be encrypted and targets several resource servers, but '{reader.Identifier}' "
                        + "decrypts the tokens issued for it and an encrypted token has a single recipient; request a "
                        + "token for each resource server separately."
                )
                .AsException();
        }

        return SelectEncryptionCredentials(
            openIdContext,
            settings,
            reader.SecretsProvider.Collection,
            $"The access token's encryption settings permit none of the keys the resource server '{reader.Identifier}' "
                + "decrypts with."
        );
    }

    // A resource server decrypts the tokens issued for it when it holds a key it can decrypt with under its own effective
    // settings — the opt-in to per-audience encryption (ADR-0075). The server never compresses what it encrypts.
    private bool DecryptsItsOwnTokens(ResolvedResourceServer resourceServer)
    {
        var ownSettings = resourceServer.EffectiveSettings;

        return CredentialSelector.TryGetEncryptionCredentials(
            AlgorithmCollectionProvider.Collection,
            ownSettings.GetPermittedAlgorithms(
                OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported
            ),
            ownSettings.GetPermittedAlgorithms(
                OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported
            ),
            [],
            resourceServer.SecretsProvider.Collection,
            out _
        );
    }

    private JoseEncryptionCredentials SelectEncryptionCredentials(
        OpenIdContext openIdContext,
        IReadOnlySettingCollection settings,
        IReadOnlyCollection<SecretKey> candidateKeys,
        string failureDescription
    )
    {
        if (
            !CredentialSelector.TryGetEncryptionCredentials(
                AlgorithmCollectionProvider.Collection,
                settings.GetPermittedAlgorithms(
                    OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported
                ),
                settings.GetPermittedAlgorithms(
                    OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported
                ),
                [],
                candidateKeys,
                out var encryptionCredentials
            )
        )
        {
            throw CreateServerError(openIdContext, failureDescription);
        }

        return encryptionCredentials;
    }

    private static string Describe(IEnumerable<string> values)
    {
        var quoted = values.Select(value => $"'{value}'").ToList();
        return quoted.Count == 0 ? "none" : string.Join(", ", quoted);
    }

    private static OpenIdException CreateServerError(
        OpenIdContext openIdContext,
        string description
    ) =>
        openIdContext
            .ErrorFactory.Create(OpenIdConstants.ErrorCodes.ServerError)
            .WithDescription(description)
            .WithStatusCode(StatusCodes.Status500InternalServerError)
            .AsException();
}
