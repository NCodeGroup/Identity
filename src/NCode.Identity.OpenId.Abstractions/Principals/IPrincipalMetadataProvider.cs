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

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Resolves a principal's server-owned <see cref="PrincipalMetadata"/> by principal id, applying the tenant's freshness
/// policy (the <c>subject_metadata_cache_duration</c> setting). This is the single read seam the subject-claims
/// projection handlers consume at issuance, so metadata flows identically for every grant flow and connection kind
/// (ADR-0055). The underlying store is the pluggable persistence seam; a host may replace this provider to source
/// metadata from anywhere.
/// </summary>
[PublicAPI]
public interface IPrincipalMetadataProvider
{
    /// <summary>
    /// Resolves the <see cref="PrincipalMetadata"/> for the given principal, honoring the tenant's freshness policy:
    /// a zero cache duration reloads on every call, a positive finite duration serves a cached value within that
    /// window, and an infinite duration serves a cached snapshot for the process lifetime.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, whose tenant supplies the
    /// freshness policy and ambient scope.</param>
    /// <param name="principalId">The server-owned principal id whose metadata is resolved (the <c>sub</c> value carried
    /// on the subject at issuance).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// <see cref="PrincipalMetadata"/>, or <see cref="PrincipalMetadata.Empty"/> when none is recorded.</returns>
    ValueTask<PrincipalMetadata> GetMetadataAsync(
        OpenIdContext openIdContext,
        string principalId,
        CancellationToken cancellationToken
    );
}
