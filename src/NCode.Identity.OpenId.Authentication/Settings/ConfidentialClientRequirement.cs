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
/// Requires a confidential (authenticated) client: a public client — one whose <c>token_endpoint_auth_method</c> is
/// unset or <c>none</c> — is rejected at the authorization endpoint.
/// </summary>
internal sealed class ConfidentialClientRequirement : IAuthorizationRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        var authMethod = context.Client.Settings.GetValue(
            OpenIdSettingKeys.TokenEndpointAuthMethod
        );
        if (
            string.IsNullOrEmpty(authMethod)
            || string.Equals(
                authMethod,
                OpenIdConstants.ClientAuthenticationMethods.None,
                StringComparison.Ordinal
            )
        )
        {
            throw context
                .OpenIdContext.ErrorFactory.UnauthorizedClient(
                    "The active security profile requires a confidential client."
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
