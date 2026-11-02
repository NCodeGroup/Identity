// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;

/// <summary>
/// Contributes the OpenID Connect Session Management 1.0 <c>check_session_iframe</c> discovery metadata, but only when
/// the tenant's <c>session_management_supported</c> setting is enabled. Session Management is available but disabled by
/// default (it depends on third-party cookies the browser platform is removing).
/// </summary>
// See ADR-1088.
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
