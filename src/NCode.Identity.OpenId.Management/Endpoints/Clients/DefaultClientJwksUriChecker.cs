// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientJwksUriChecker"/>, which asks the
/// <see cref="IOutboundHostChecker"/> whether the outbound network guard refuses the host of a <c>jwks_uri</c>.
/// </summary>
// See ADR-1106.
internal sealed class DefaultClientJwksUriChecker(IOutboundHostChecker outboundHostChecker)
    : IClientJwksUriChecker
{
    private IOutboundHostChecker OutboundHostChecker { get; } = outboundHostChecker;

    /// <inheritdoc />
    public async ValueTask<SettingError?> GetRefusalAsync(
        JsonElement settings,
        JsonElement? storedSettings,
        CancellationToken cancellationToken
    )
    {
        if (
            settings.ValueKind != JsonValueKind.Object
            || !settings.TryGetPropertyValue<string>(OpenIdSettingNames.JwksUri, out var jwksUri)
            || string.IsNullOrEmpty(jwksUri)
        )
        {
            return null;
        }

        if (
            storedSettings is { ValueKind: JsonValueKind.Object } stored
            && stored.TryGetPropertyValue<string>(OpenIdSettingNames.JwksUri, out var storedJwksUri)
            && string.Equals(jwksUri, storedJwksUri, StringComparison.Ordinal)
        )
        {
            return null;
        }

        if (
            !Uri.TryCreate(jwksUri, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            return null;
        }

        var isRefused = await OutboundHostChecker.IsRefusedAsync(
            uri.DnsSafeHost,
            cancellationToken
        );
        return isRefused
            ? new SettingError(
                OpenIdSettingNames.JwksUri,
                "The 'jwks_uri' host resolves only to addresses the server's outbound network guard refuses, so the "
                    + "server can never fetch it. A client on a private network registers no 'jwks_uri' and imports "
                    + "its public key through POST /api/tenants/{tenantId}/clients/{clientId}/secrets/import."
            )
            : null;
    }
}
