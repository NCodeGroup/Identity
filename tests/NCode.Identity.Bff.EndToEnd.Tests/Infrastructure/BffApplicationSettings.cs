// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Describes one backend-for-frontend (BFF) application that a <see cref="BffApplication"/> hosts.
/// </summary>
/// <param name="ClientId">The client identifier that the BFF is registered under at the OpenID Provider.</param>
/// <param name="Port">The loopback port that the BFF listens on over HTTPS.</param>
/// <param name="SameSite">The <c>SameSite</c> mode of the session cookie.</param>
/// <param name="Authority">The issuer of the OpenID Provider.</param>
/// <param name="Certificate">The certificate that the BFF serves and that it trusts from the OpenID Provider.</param>
/// <param name="CorsOrigins">The origins that the CORS policy admits.</param>
/// <param name="TrustedOrigins">The origins that Fetch Metadata treats as same-site, as <c>Bff:TrustedOrigins</c>.</param>
public sealed record BffApplicationSettings(
    string ClientId,
    int Port,
    SameSiteMode SameSite,
    string Authority,
    TestCertificate Certificate,
    IReadOnlyList<string> CorsOrigins,
    IReadOnlyList<string> TrustedOrigins
);
