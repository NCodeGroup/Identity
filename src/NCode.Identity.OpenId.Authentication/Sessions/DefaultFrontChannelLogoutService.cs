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
        var sessionId = subject.SessionId;

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
