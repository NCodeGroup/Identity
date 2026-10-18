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

using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides the default implementation of <see cref="ISubjectIdentifierService"/>. The pairwise projection is a
/// stateless, deterministic derivation (OpenID Connect Core 1.0 §8.1): the SHA-256 hash of the client's pairwise
/// sector identifier, the stable principal identifier, the tenant identifier, and an optional secret salt, encoded as
/// Base64Url. Because it is deterministic, the same <c>(sector, principal)</c> pair always yields the same pseudonym
/// without a persisted mapping.
/// </summary>
internal class DefaultSubjectIdentifierService(ICryptoService cryptoService)
    : ISubjectIdentifierService
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public string ProjectSubjectId(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string principalId
    )
    {
        var settings = openIdClient.Settings;

        var subjectType =
            settings.TryGetValue(OpenIdSettingKeys.SubjectType, out var configuredSubjectType)
            && !string.IsNullOrEmpty(configuredSubjectType)
                ? configuredSubjectType
                : OpenIdConstants.SubjectTypes.Public;

        if (
            !string.Equals(
                subjectType,
                OpenIdConstants.SubjectTypes.Pairwise,
                StringComparison.Ordinal
            )
        )
        {
            // public (the default) returns the stable principal identifier unchanged.
            return principalId;
        }

        var sector = ResolveSectorIdentifier(openIdClient);

        var salt = settings.TryGetValue(OpenIdSettingKeys.PairwiseSalt, out var configuredSalt)
            ? configuredSalt
            : string.Empty;

        // A NUL separator is used because it cannot appear in any of the (URL-safe id / host) component values, so the
        // composed input is unambiguous. The tenant id isolates tenants even when no salt is configured.
        var composed = string.Concat(
            sector,
            "\0",
            principalId,
            "\0",
            openIdContext.Tenant.TenantId,
            "\0",
            salt
        );

        return CryptoService.HashValue(
            composed,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64Url
        );
    }

    /// <summary>
    /// Resolves the pairwise sector identifier for the client (OpenID Connect Core 1.0 §8.1): the host of the
    /// configured <c>sector_identifier_uri</c> when present; otherwise the common host of the client's
    /// <c>redirect_uris</c>; falling back to the <c>client_id</c> when the redirect hosts are absent or ambiguous.
    /// </summary>
    private static string ResolveSectorIdentifier(OpenIdClient openIdClient)
    {
        if (
            openIdClient.Settings.TryGetValue(
                OpenIdSettingKeys.SectorIdentifierUri,
                out var sectorIdentifierUri
            ) && TryGetHost(sectorIdentifierUri, out var sectorHost)
        )
        {
            return sectorHost;
        }

        string? commonHost = null;
        foreach (var redirectUri in openIdClient.RedirectUris)
        {
            if (!TryGetHost(redirectUri, out var host))
            {
                continue;
            }

            if (commonHost is null)
            {
                commonHost = host;
            }
            else if (!string.Equals(commonHost, host, StringComparison.OrdinalIgnoreCase))
            {
                // Multiple distinct redirect hosts without a sector_identifier_uri are ambiguous; treat the client as
                // its own sector so each client still receives a stable, non-correlatable pairwise subject.
                return openIdClient.ClientId;
            }
        }

        return commonHost ?? openIdClient.ClientId;
    }

    private static bool TryGetHost(string? value, out string host)
    {
        if (
            !string.IsNullOrEmpty(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && !string.IsNullOrEmpty(uri.Host)
        )
        {
            host = uri.Host;
            return true;
        }

        host = string.Empty;
        return false;
    }
}
