// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
