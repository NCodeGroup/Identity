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
