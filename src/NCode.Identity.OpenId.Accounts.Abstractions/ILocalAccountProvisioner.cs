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

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Provides the seam through which a host provisions (creates) local accounts — the server-owned credential, profile,
/// and status records behind self-issued connections. It is the write counterpart to <see cref="ILocalAccountSource"/>
/// (which reads and validates): an implementation hashes the credential, generates the opaque account id and security
/// stamp, and persists the account. A reference implementation is supplied by an opt-in persistence package.
/// </summary>
[PublicAPI]
public interface ILocalAccountProvisioner
{
    /// <summary>
    /// Creates a new local account in the ambient tenant from the supplied credential and profile, returning the
    /// server-generated identifier (the self-issued connection's subject, also the account's
    /// <see cref="LocalAccount.Subject"/>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, carrying the owning tenant.</param>
    /// <param name="request">The account creation request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the server-generated
    /// account identifier.</returns>
    ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        LocalAccountCreationRequest request,
        CancellationToken cancellationToken
    );
}
