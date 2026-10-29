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

using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client.Options;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Validates that <see cref="BffTokenOptions.RefreshLeaseDuration"/> outlasts the protocol client's
/// <see cref="OpenIdClientOptions.RequestTimeout"/>. The composition root makes this check because only it knows both
/// the BFF and the protocol client: a DPoP nonce retry sends the refresh request twice under one lease, so a shorter
/// lease could expire while the rotated refresh token is still in flight.
/// </summary>
/// <param name="clientOptions">The options of the protocol client that sends the refresh request.</param>
// See ADR-0291.
internal sealed class BffRefreshLeaseValidator(IOptions<OpenIdClientOptions> clientOptions)
    : IValidateOptions<BffOptions>
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Tokens is null)
            return ValidateOptionsResult.Success;

        var lease = options.Tokens.RefreshLeaseDuration;
        var minimum = clientOptions.Value.RequestTimeout * 2 + Margin;
        if (lease >= minimum)
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"Bff:Tokens:RefreshLeaseDuration ({lease}) must be at least twice OpenIdClientOptions.RequestTimeout plus 10 seconds ({minimum}), because a DPoP nonce retry sends the refresh request twice under one lease."
        );
    }
}
