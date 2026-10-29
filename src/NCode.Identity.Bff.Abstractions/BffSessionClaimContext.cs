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

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose;

namespace NCode.Identity.Bff;

/// <summary>
/// The context in which the <see cref="IBffSessionClaimContributor"/> strategies shape the claims of a session at login.
/// It starts with the claims of the validated ID token. The claims that anchor the session to the provider's
/// authentication (<see cref="ReservedClaimTypes"/>) can be neither added nor removed, so no contributor can change
/// who the session is for or how the user authenticated.
/// </summary>
// See ADR-0290.
[PublicAPI]
public sealed class BffSessionClaimContext
{
    private readonly List<Claim> _claims;

    /// <summary>
    /// Gets the claim types that a contributor can neither add nor remove: <c>iss</c>, <c>sub</c>, <c>sid</c>,
    /// <c>auth_time</c>, <c>acr</c> and <c>amr</c>, which the provider asserted about the authentication.
    /// </summary>
    public static IReadOnlySet<string> ReservedClaimTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Iss,
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.Sid,
            JoseClaimNames.Payload.AuthTime,
            JoseClaimNames.Payload.Acr,
            JoseClaimNames.Payload.Amr,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="BffSessionClaimContext"/> class.
    /// </summary>
    /// <param name="httpContext">The HTTP context of the login callback.</param>
    /// <param name="issuer">The issuer of the provider that authenticated the user.</param>
    /// <param name="subject">The subject the session is for.</param>
    /// <param name="claims">The claims the session starts with: those of the validated ID token.</param>
    public BffSessionClaimContext(
        HttpContext httpContext,
        string issuer,
        string subject,
        IEnumerable<Claim> claims
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(claims);

        HttpContext = httpContext;
        Issuer = issuer;
        Subject = subject;
        _claims = [.. claims];
    }

    /// <summary>
    /// Gets the HTTP context of the login callback.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the issuer of the provider that authenticated the user.
    /// </summary>
    public string Issuer { get; }

    /// <summary>
    /// Gets the subject the session is for.
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the claims the session will be created with, as the contributors have shaped them so far. A session stores
    /// each claim's type, value and value type; every claim of a session carries the provider's issuer.
    /// </summary>
    public IReadOnlyList<Claim> Claims => _claims;

    /// <summary>
    /// Adds a claim to the session.
    /// </summary>
    /// <param name="claim">The claim to add.</param>
    /// <exception cref="ArgumentException">The claim's type is one of the <see cref="ReservedClaimTypes"/>.</exception>
    public void AddClaim(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ThrowIfReserved(claim.Type, nameof(claim));

        _claims.Add(claim);
    }

    /// <summary>
    /// Removes every claim of a type from the session.
    /// </summary>
    /// <param name="claimType">The type of the claims to remove.</param>
    /// <returns>The number of claims removed.</returns>
    /// <exception cref="ArgumentException">The type is one of the <see cref="ReservedClaimTypes"/>.</exception>
    public int RemoveClaims(string claimType)
    {
        ArgumentNullException.ThrowIfNull(claimType);
        ThrowIfReserved(claimType, nameof(claimType));

        return _claims.RemoveAll(claim =>
            string.Equals(claim.Type, claimType, StringComparison.Ordinal)
        );
    }

    private static void ThrowIfReserved(string claimType, string paramName)
    {
        if (ReservedClaimTypes.Contains(claimType))
            throw new ArgumentException(
                $"The claim type '{claimType}' anchors the session to the provider's authentication and cannot be changed.",
                paramName
            );
    }
}
