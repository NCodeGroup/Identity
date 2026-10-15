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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;

/// <summary>
/// Validates a <c>subject_token</c> or <c>actor_token</c> presented to an OAuth 2.0 Token Exchange
/// (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>) and resolves it to a
/// <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// </summary>
/// <remarks>
/// This is the trust-boundary seam for token exchange. The default implementation validates only tokens this
/// authorization server itself issued (self-issued JWTs validated against the tenant's own signing keys and issuer).
/// A host that federates with external/enterprise identity providers replaces or decorates this service to additionally
/// accept tokens minted by trusted upstream issuers.
/// </remarks>
[PublicAPI]
public interface ISubjectTokenValidator
{
    /// <summary>
    /// Validates the presented token and resolves it to a <see cref="System.Security.Claims.ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> making the token-exchange request.</param>
    /// <param name="token">The encoded token value to validate.</param>
    /// <param name="tokenTypeIdentifier">The RFC 8693 token type identifier that describes <paramref name="token"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="SubjectTokenValidationResult"/>.</returns>
    ValueTask<SubjectTokenValidationResult> ValidateAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string token,
        string tokenTypeIdentifier,
        CancellationToken cancellationToken
    );
}
