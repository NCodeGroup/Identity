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
            subjectAuthentication.SubjectId,
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
