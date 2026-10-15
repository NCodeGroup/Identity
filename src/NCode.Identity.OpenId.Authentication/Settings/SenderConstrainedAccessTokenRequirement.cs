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
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires a sender-constrained access token: the token request must present either a DPoP proof (RFC 9449) or a bound
/// client certificate (RFC 8705).
/// </summary>
internal sealed class SenderConstrainedAccessTokenRequirement : ITokenRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        TokenRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        if (!context.Confirmation.HasValue)
        {
            throw context
                .OpenIdContext.ErrorFactory.InvalidRequest(
                    "The active security profile requires a sender-constrained access token (DPoP or mutual-TLS)."
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
