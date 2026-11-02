// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;

/// <summary>
/// Provides a default <see cref="GetUserInfoClaimsCommand"/> handler that projects the principal's metadata bags into
/// the UserInfo response as nested JSON objects, gated by the <c>send_profile_metadata_in_user_info</c> and
/// <c>send_system_metadata_in_user_info</c> settings. The bags are resolved fresh from the
/// <see cref="IPrincipalMetadataProvider"/> by principal id, so they flow identically for every connection kind. This is
/// only the built-in default projection; applications register their own pipeline handlers to shape metadata into
/// bespoke claims.
/// </summary>
internal class DefaultGetUserInfoMetadataClaimsHandler(IPrincipalMetadataProvider metadataProvider)
    : ICommandHandler<GetUserInfoClaimsCommand>,
        ISupportMediatorPriority
{
    private IPrincipalMetadataProvider MetadataProvider { get; } = metadataProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subjectAuthentication, claims) = command;

        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var sendProfile = settings.GetValue(OpenIdSettingKeys.SendProfileMetadataInUserInfo);
        var sendSystem = settings.GetValue(OpenIdSettingKeys.SendSystemMetadataInUserInfo);
        if (!sendProfile && !sendSystem)
        {
            return;
        }

        var metadata = await MetadataProvider.GetMetadataAsync(
            openIdContext,
            subjectAuthentication.PrincipalId,
            cancellationToken
        );

        if (sendProfile)
        {
            AddMetadata(
                claims,
                SubjectMetadataClaimTypes.ProfileMetadata,
                metadata.ProfileMetadata
            );
        }

        if (sendSystem)
        {
            AddMetadata(claims, SubjectMetadataClaimTypes.SystemMetadata, metadata.SystemMetadata);
        }
    }

    private static void AddMetadata(
        IDictionary<string, object> claims,
        string claimType,
        JsonElement bag
    )
    {
        // Skip an absent or empty bag, and do not override a claim another enricher already supplied.
        if (bag.ValueKind != JsonValueKind.Object || !bag.EnumerateObject().MoveNext())
        {
            return;
        }

        if (claims.ContainsKey(claimType))
        {
            return;
        }

        claims[claimType] = bag;
    }
}
