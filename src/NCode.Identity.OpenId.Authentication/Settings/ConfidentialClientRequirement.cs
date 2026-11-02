// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires a confidential client: a client registered as public — one whose <c>token_endpoint_auth_method</c> is
/// <c>none</c> — is rejected at the authorization endpoint.
/// </summary>
internal sealed class ConfidentialClientRequirement : IAuthorizationRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        if (!context.Client.IsRegisteredAsConfidential)
        {
            throw context
                .OpenIdContext.ErrorFactory.UnauthorizedClient(
                    "A selected security profile requires a confidential client."
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
