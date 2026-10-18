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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides the ability to perform the OpenID Connect Core 1.0 §8.1 registration-time validation of a client's
/// <c>sector_identifier_uri</c>: fetching the JSON document it references and verifying that it lists every one of the
/// client's registered redirect URIs. This is the deployment-specific seam for the fetch and its SSRF controls; a host
/// may replace the default implementation to change how the document is retrieved or validated.
/// </summary>
[PublicAPI]
public interface ISectorIdentifierValidator
{
    /// <summary>
    /// Validates the client's <c>sector_identifier_uri</c> against its registered redirect URIs by fetching the
    /// referenced JSON document and confirming it includes each redirect URI (OpenID Connect Core 1.0 §8.1). The fetch
    /// is guarded against server-side request forgery.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="sectorIdentifierUri">The client's <c>sector_identifier_uri</c> value to fetch and validate.</param>
    /// <param name="redirectUris">The client's registered redirect URIs that the document must include.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <see langword="null"/>
    /// when the <c>sector_identifier_uri</c> is valid; otherwise, an <see cref="IOpenIdError"/> describing the failure.</returns>
    ValueTask<IOpenIdError?> ValidateAsync(
        OpenIdContext openIdContext,
        string sectorIdentifierUri,
        IReadOnlyCollection<string> redirectUris,
        CancellationToken cancellationToken
    );
}
