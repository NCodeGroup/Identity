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

using System.Diagnostics;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Handles <see cref="CertificateLifetimeRequirement"/> by asserting the signing certificate (if any) is non-expired.
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

        var notBefore = certificate.NotBefore.WithPrecisionInSeconds();
        var notBeforeTicks = notBefore.Ticks;

        var notAfter = certificate.NotAfter.WithPrecisionInSeconds();
        var notAfterTicks = notAfter.Ticks;

        if (notBeforeTicks > nowTicks + clockSkewTicks)
        {
            context.Fail($"The certificate is not valid before {notBefore}.");
            return ValueTask.CompletedTask;
        }

        if (notAfterTicks < nowTicks - clockSkewTicks)
        {
            context.Fail($"The certificate is not valid after {notAfter}.");
            return ValueTask.CompletedTask;
        }

        context.Succeed(requirement);
        return ValueTask.CompletedTask;
    }
}
