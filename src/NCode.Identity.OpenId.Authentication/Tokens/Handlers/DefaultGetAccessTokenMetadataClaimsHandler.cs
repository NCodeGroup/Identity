// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that projects the principal's metadata
/// bags into the access token, gated by the <c>send_profile_metadata_in_access_token</c> and
/// <c>send_system_metadata_in_access_token</c> settings. The bags are resolved fresh from the
/// <see cref="IPrincipalMetadataProvider"/> by principal id at issuance, so they flow identically for every grant flow
/// and connection kind. The access token is the authorization-appropriate destination for server-controlled system
/// metadata. This is only the built-in default projection; applications register their own pipeline handlers to shape
/// metadata into bespoke claims.
/// </summary>
internal class DefaultGetAccessTokenMetadataClaimsHandler(
    IPrincipalMetadataProvider metadataProvider
) : ICommandHandler<GetAccessTokenSubjectClaimsCommand>, ISupportMediatorPriority
{
    private IPrincipalMetadataProvider MetadataProvider { get; } = metadataProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        GetAccessTokenSubjectClaimsCommand command,
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
        var sendProfile = settings.GetValue(OpenIdSettingKeys.SendProfileMetadataInAccessToken);
        var sendSystem = settings.GetValue(OpenIdSettingKeys.SendSystemMetadataInAccessToken);
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
                SubjectMetadataClaimTypes.ProfileMetadata,
                metadata.ProfileMetadata
            );
        }

        if (sendSystem)
        {
            SubjectMetadataClaims.AddMetadataClaim(
                targetClaims,
                SubjectMetadataClaimTypes.SystemMetadata,
                metadata.SystemMetadata
            );
        }
    }
}
