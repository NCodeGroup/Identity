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

using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires a request object to be addressed to the issuer identifier (FAPI 1.0 Advanced §5.2.2, FAPI 2.0 Message
/// Signing §5.3.1) by refusing a request object from a client whose settings name another audience in
/// <c>request_object_expected_audience</c>, which is client-scoped and replace-merged, so no ceiling can cap it.
/// </summary>
internal sealed class IssuerOnlyRequestObjectAudienceRequirement
    : IAuthorizationRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            context.Request.OriginalRequestObject is not null
            && !string.IsNullOrEmpty(
                context.Client.Settings.GetValue(OpenIdSettingKeys.RequestObjectExpectedAudience)
            )
        )
        {
            throw context
                .OpenIdContext.ErrorFactory.InvalidRequest(
                    "Under a selected security profile a request object must be addressed to the issuer identifier, so the client must not set 'request_object_expected_audience'.",
                    OpenIdProtocol.ErrorCodes.InvalidRequestJwt
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
