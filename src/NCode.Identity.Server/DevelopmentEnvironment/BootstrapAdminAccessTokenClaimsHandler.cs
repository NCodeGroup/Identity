// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Management;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A token-issuance handler that stamps the <c>GlobalAdmin</c> role claim onto the access tokens of the configured
/// bootstrap administrator client in the configured bootstrap tenant. This is the cold-start bridge: the claim-based
/// <c>GlobalAdminHandler</c> is unchanged, and the role is conferred only on the one configured client, so a client that
/// shares its identifier in another tenant gains nothing.
/// </summary>
internal sealed class BootstrapAdminAccessTokenClaimsHandler(
    IOptions<BootstrapAdminOptions> optionsAccessor
) : ICommandHandler<GetAccessTokenPayloadClaimsCommand>
{
    private BootstrapAdminOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetAccessTokenPayloadClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var bootstrapClientId = Options.ClientId;
        if (string.IsNullOrEmpty(bootstrapClientId))
        {
            return ValueTask.CompletedTask;
        }

        var (openIdContext, openIdClient, _, payloadClaims) = command;
        var isBootstrapClient =
            string.Equals(openIdClient.ClientId, bootstrapClientId, StringComparison.Ordinal)
            && string.Equals(
                openIdContext.Tenant.TenantId,
                Options.TenantId,
                StringComparison.Ordinal
            );
        if (!isBootstrapClient)
        {
            return ValueTask.CompletedTask;
        }

        payloadClaims[JoseClaimNames.Payload.Role] = BuiltInRoles.GlobalAdmin;
        return ValueTask.CompletedTask;
    }
}
