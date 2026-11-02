// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of the <see cref="IFrontChannelLogoutService"/> abstraction (OpenID Connect
/// Front-Channel Logout 1.0). It resolves the <c>frontchannel_logout_uri</c> of each participating client and appends
/// the <c>iss</c>/<c>sid</c> query parameters for clients that require them.
/// </summary>
internal class DefaultFrontChannelLogoutService(IOpenIdClientResolver clientResolver)
    : IFrontChannelLogoutService
{
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> GetLogoutUrisAsync(
        OpenIdContext openIdContext,
        EndSessionSubject subject,
        CancellationToken cancellationToken
    )
    {
        if (subject.ParticipatingClientIds.Count == 0)
        {
            return [];
        }

        var issuer = openIdContext.Tenant.Issuer;

        // The session identifier is appended only when the tenant advertises that it appends one (OpenID Connect
        // Front-Channel Logout 1.0 §3).
        var sessionSupported = openIdContext.Tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.FrontChannelLogoutSessionSupported
        );
        var sessionId = sessionSupported ? subject.SessionId : null;

        var logoutUris = new List<string>(subject.ParticipatingClientIds.Count);
        foreach (var clientId in subject.ParticipatingClientIds)
        {
            var client = await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                clientId,
                cancellationToken
            );
            if (client is null)
            {
                continue;
            }

            if (
                !client.Settings.TryGetValue(
                    OpenIdSettingKeys.FrontChannelLogoutUri,
                    out var frontChannelLogoutUri
                ) || string.IsNullOrEmpty(frontChannelLogoutUri)
            )
            {
                continue;
            }

            var sessionRequired = client.Settings.GetValue(
                OpenIdSettingKeys.FrontChannelLogoutSessionRequired
            );
            if (sessionRequired && !string.IsNullOrEmpty(sessionId))
            {
                var separator = frontChannelLogoutUri.Contains('?', StringComparison.Ordinal)
                    ? '&'
                    : '?';
                frontChannelLogoutUri =
                    $"{frontChannelLogoutUri}{separator}iss={Uri.EscapeDataString(issuer)}"
                    + $"&sid={Uri.EscapeDataString(sessionId)}";
            }

            logoutUris.Add(frontChannelLogoutUri);
        }

        return logoutUris;
    }
}
