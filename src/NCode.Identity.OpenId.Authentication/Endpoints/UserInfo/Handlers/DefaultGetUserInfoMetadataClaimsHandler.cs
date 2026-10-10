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
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;

/// <summary>
/// Provides a default <see cref="GetUserInfoClaimsCommand"/> handler that projects an account's metadata bags into the
/// UserInfo response as nested JSON objects, gated by the <c>send_profile_metadata_in_user_info</c> and
/// <c>send_system_metadata_in_user_info</c> settings. This is only the built-in default projection; applications
/// register their own pipeline handlers to shape metadata into bespoke claims.
/// </summary>
internal class DefaultGetUserInfoMetadataClaimsHandler
    : ICommandHandler<GetUserInfoClaimsCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subjectAuthentication, claims) = command;

        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var subject = subjectAuthentication.Subject;

        if (settings.GetValue(OpenIdSettingKeys.SendProfileMetadataInUserInfo))
        {
            AddMetadata(subject, AccountConstants.ProfileMetadataClaimType, claims);
        }

        if (settings.GetValue(OpenIdSettingKeys.SendSystemMetadataInUserInfo))
        {
            AddMetadata(subject, AccountConstants.SystemMetadataClaimType, claims);
        }

        return ValueTask.CompletedTask;
    }

    private static void AddMetadata(
        System.Security.Claims.ClaimsPrincipal subject,
        string claimType,
        IDictionary<string, object> claims
    )
    {
        // Do not override a claim another enricher already supplied.
        if (claims.ContainsKey(claimType))
        {
            return;
        }

        if (subject.TryGetMetadata(claimType, out var metadata))
        {
            claims[claimType] = metadata;
        }
    }
}
