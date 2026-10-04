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

using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Management;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// A token-issuance handler that stamps the <c>GlobalAdmin</c> role claim onto the access tokens of the configured
/// bootstrap administrator client. This is the cold-start bridge (ADR-0044): the claim-based
/// <c>GlobalAdminHandler</c> is unchanged, and the role is conferred only on the one configured client.
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

        var (_, openIdClient, _, payloadClaims) = command;
        if (!string.Equals(openIdClient.ClientId, bootstrapClientId, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        payloadClaims[JoseClaimNames.Payload.Role] = BuiltInRoles.GlobalAdmin;
        return ValueTask.CompletedTask;
    }
}
