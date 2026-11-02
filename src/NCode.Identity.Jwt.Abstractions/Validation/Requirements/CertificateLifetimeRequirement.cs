// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// A requirement that, when the signing key is a certificate, asserts the certificate is active and non-expired,
/// allowing for the configured <see cref="ClockSkew"/>.
/// </summary>
/// <param name="ClockSkew">The amount of time to allow for clock skew when comparing certificate validity dates.</param>
[PublicAPI]
public sealed record CertificateLifetimeRequirement(TimeSpan ClockSkew) : IJwtValidationRequirement;
