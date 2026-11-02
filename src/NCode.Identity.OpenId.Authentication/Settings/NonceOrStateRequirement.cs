// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires a request to carry the value its client uses to detect a forged or replayed response: <c>nonce</c> when it
/// requests the <c>openid</c> scope, since the ID token binds it, and <c>state</c> otherwise (FAPI 1.0 Advanced
/// §5.2.2.2 and §5.2.2.3).
/// </summary>
internal sealed class NonceOrStateRequirement : IAuthorizationRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        var request = context.Request;
        var requestsOpenId = request.Scopes.Contains(
            OpenIdProtocol.ScopeTypes.OpenId,
            StringComparer.Ordinal
        );

        var (parameterName, value) = requestsOpenId
            ? (OpenIdProtocol.Parameters.Nonce, request.Nonce)
            : (OpenIdProtocol.Parameters.State, request.State);

        if (string.IsNullOrEmpty(value))
        {
            throw context.OpenIdContext.ErrorFactory.MissingParameter(parameterName).AsException();
        }

        return ValueTask.CompletedTask;
    }
}
