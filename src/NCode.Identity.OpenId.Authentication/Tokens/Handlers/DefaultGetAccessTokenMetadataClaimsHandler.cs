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

using NCode.Identity.Claims;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that projects an account's metadata bags
/// into the access token, gated by the <c>send_profile_metadata_in_access_token</c> and
/// <c>send_system_metadata_in_access_token</c> settings. The access token is the authorization-appropriate destination
/// for server-controlled system metadata. This is only the built-in default projection; applications register their own
/// pipeline handlers to shape metadata into bespoke claims.
/// </summary>
internal class DefaultGetAccessTokenMetadataClaimsHandler(IClaimsService claimsService)
    : ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
        ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetAccessTokenSubjectClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, openIdClient, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return ValueTask.CompletedTask;
        }

        var (_, _, sourceClaims, _) = tokenRequest.SubjectAuthentication.Value;
        var settings = openIdClient.Settings;

        var claimTypes = new List<string>(capacity: 2);

        if (settings.GetValue(OpenIdSettingKeys.SendProfileMetadataInAccessToken))
        {
            claimTypes.Add(AccountConstants.ProfileMetadataClaimType);
        }

        if (settings.GetValue(OpenIdSettingKeys.SendSystemMetadataInAccessToken))
        {
            claimTypes.Add(AccountConstants.SystemMetadataClaimType);
        }

        if (claimTypes.Count > 0)
        {
            // CopyClaims preserves the JSON value type, so each bag re-serializes as a nested object in the token.
            ClaimsService.CopyClaims(
                sourceClaims,
                targetClaims,
                preventDuplicates: true,
                claimTypes
            );
        }

        return ValueTask.CompletedTask;
    }
}
