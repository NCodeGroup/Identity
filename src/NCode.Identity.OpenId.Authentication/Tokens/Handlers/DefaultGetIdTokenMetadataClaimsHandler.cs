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

using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default <see cref="GetIdTokenSubjectClaimsCommand"/> handler that projects the principal's metadata bags
/// into the id token, gated by the <c>send_profile_metadata_in_id_token</c> and <c>send_system_metadata_in_id_token</c>
/// settings. The bags are resolved fresh from the <see cref="IPrincipalMetadataProvider"/> by principal id at issuance,
/// so they flow identically for every grant flow and connection kind. This is only the built-in default projection;
/// applications register their own pipeline handlers to shape metadata into bespoke claims.
/// </summary>
internal class DefaultGetIdTokenMetadataClaimsHandler(IPrincipalMetadataProvider metadataProvider)
    : ICommandHandler<GetIdTokenSubjectClaimsCommand>,
        ISupportMediatorPriority
{
    private IPrincipalMetadataProvider MetadataProvider { get; } = metadataProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        GetIdTokenSubjectClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return;
        }

        var settings = openIdClient.Settings;
        var sendProfile = settings.GetValue(OpenIdSettingKeys.SendProfileMetadataInIdToken);
        var sendSystem = settings.GetValue(OpenIdSettingKeys.SendSystemMetadataInIdToken);
        if (!sendProfile && !sendSystem)
        {
            return;
        }

        var (_, _, _, principalId) = tokenRequest.SubjectAuthentication.Value;
        var metadata = await MetadataProvider.GetMetadataAsync(
            openIdContext,
            principalId,
            cancellationToken
        );

        if (sendProfile)
        {
            SubjectMetadataClaims.AddMetadataClaim(
                targetClaims,
                AccountConstants.ProfileMetadataClaimType,
                metadata.ProfileMetadata
            );
        }

        if (sendSystem)
        {
            SubjectMetadataClaims.AddMetadataClaim(
                targetClaims,
                AccountConstants.SystemMetadataClaimType,
                metadata.SystemMetadata
            );
        }
    }
}
