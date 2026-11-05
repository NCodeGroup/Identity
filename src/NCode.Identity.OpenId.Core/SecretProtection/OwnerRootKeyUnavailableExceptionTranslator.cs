// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Translates an <see cref="OwnerRootKeyUnavailableException"/> into the <c>temporarily_unavailable</c> error that an
/// endpoint answers with <c>503 Service Unavailable</c> (RFC 9110 §15.6.4), a <c>Retry-After</c> of the owner root key
/// retry interval rounded up to whole seconds (RFC 9110 §10.2.3), and <c>Cache-Control: no-store</c>.
/// </summary>
// See ADR-1428.
internal sealed class OwnerRootKeyUnavailableExceptionTranslator(
    IOptions<SecretProtectionOptions> options
) : IOpenIdExceptionTranslator
{
    private IOptions<SecretProtectionOptions> Options { get; } = options;

    /// <inheritdoc />
    public bool TryTranslate(
        OpenIdEnvironment openIdEnvironment,
        Exception exception,
        [NotNullWhen(true)] out IOpenIdError? error
    )
    {
        if (exception is not OwnerRootKeyUnavailableException)
        {
            error = null;
            return false;
        }

        // Retry-After counts whole seconds, so round up and never tell the caller to retry at once.
        var seconds = Math.Max(
            1L,
            (long)Math.Ceiling(Options.Value.OwnerRootKeys.RetryInterval.TotalSeconds)
        );

        error = openIdEnvironment
            .CreateError(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable)
            .WithDescription(
                "The tenant's root key is unavailable, so its encryption keys cannot be used until it is restored."
            )
            .WithStatusCode(StatusCodes.Status503ServiceUnavailable)
            .WithResponseHeader(
                HeaderNames.RetryAfter,
                seconds.ToString(CultureInfo.InvariantCulture)
            )
            .WithResponseHeader(HeaderNames.CacheControl, "no-store")
            .WithException(exception);
        return true;
    }
}
