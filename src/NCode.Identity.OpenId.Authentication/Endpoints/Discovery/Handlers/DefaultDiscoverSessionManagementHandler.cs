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

using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;

/// <summary>
/// Contributes the OpenID Connect Session Management 1.0 <c>check_session_iframe</c> discovery metadata, but only when
/// the tenant's <c>session_management_supported</c> setting is enabled. Session Management is available but disabled by
/// default (it depends on third-party cookies the browser platform is removing); see
/// <see href="../../../../../docs/adr/0088-opt-in-front-channel-logout-and-session-management.md">ADR-0088</see>.
/// </summary>
internal class DefaultDiscoverSessionManagementHandler(LinkGenerator linkGenerator)
    : ICommandHandler<DiscoverMetadataCommand>,
        ISupportMediatorPriority
{
    private LinkGenerator LinkGenerator { get; } = linkGenerator;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverMetadataCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, metadata) = command;

        var enabled = openIdContext.Tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.SessionManagementSupported
        );
        if (!enabled)
        {
            return ValueTask.CompletedTask;
        }

        var checkSessionIframeUrl = LinkGenerator.GetTenantUriByName(
            openIdContext,
            OpenIdConstants.EndpointNames.CheckSessionIframe,
            new { }
        );
        if (!string.IsNullOrEmpty(checkSessionIframeUrl))
        {
            metadata.TryAdd(
                OpenIdConstants.EndpointNames.CheckSessionIframe,
                checkSessionIframeUrl
            );
        }

        return ValueTask.CompletedTask;
    }
}
