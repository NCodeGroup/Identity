// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NCode.Identity.Secrets.Keys;
using NCode.Time;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="CertificateLifetimeRequirement"/> by asserting the signing certificate (if any) is active and
/// non-expired, comparing its validity with the UTC clock.
/// </summary>
internal sealed class CertificateLifetimeRequirementHandler
    : JwtValidationHandler<CertificateLifetimeRequirement>
{
    /// <inheritdoc />
    protected override ValueTask HandleRequirementAsync(
        JwtValidationContext context,
        CertificateLifetimeRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // use 'long' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions
        var clockSkewTicks = requirement.ClockSkew.Ticks;

        if (
            context.SecretKey
            is not AsymmetricSecretKey { HasCertificate: true } asymmetricSecretKey
        )
        {
            context.Succeed(requirement);
            return ValueTask.CompletedTask;
        }

        using var certificate = asymmetricSecretKey.ExportCertificate();
        Debug.Assert(certificate is not null);

        var nowTicks = context.TimeProvider.GetTimestampWithPrecisionInSeconds();

        // X509Certificate2 reports its validity in local time, so convert it to UTC before comparing it with the UTC
        // clock; otherwise a host outside UTC shifts the window by its offset.
        var notBefore = new DateTimeOffset(
            certificate.NotBefore.ToUniversalTime()
        ).WithPrecisionInSeconds();
        var notBeforeTicks = notBefore.UtcTicks;

        var notAfter = new DateTimeOffset(
            certificate.NotAfter.ToUniversalTime()
        ).WithPrecisionInSeconds();
        var notAfterTicks = notAfter.UtcTicks;

        if (notBeforeTicks > nowTicks + clockSkewTicks)
        {
            context.Fail(
                JwtValidationFailureCodes.InvalidCertificate,
                $"The certificate is not valid before {notBefore}."
            );
            return ValueTask.CompletedTask;
        }

        if (notAfterTicks < nowTicks - clockSkewTicks)
        {
            context.Fail(
                JwtValidationFailureCodes.InvalidCertificate,
                $"The certificate is not valid after {notAfter}."
            );
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
