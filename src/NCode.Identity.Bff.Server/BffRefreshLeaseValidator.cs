// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
// See ADR-1291.
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
