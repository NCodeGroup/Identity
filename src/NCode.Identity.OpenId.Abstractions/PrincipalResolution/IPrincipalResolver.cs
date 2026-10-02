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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides an abstraction that resolves an authenticated caller to a stable, server-owned principal identifier (the
/// value authority references and that is emitted as the <c>sub</c> claim).
/// </summary>
[PublicAPI]
public interface IPrincipalResolver
{
    /// <summary>
    /// Resolves the stable principal identifier for an already-provisioned caller, without provisioning. Used by
    /// read-only paths (such as authorization) where a caller that has never been seen simply has no authority.
    /// </summary>
    /// <param name="user">The authenticated caller.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the lookup participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// principal identifier, or <c>null</c> when the caller cannot be resolved to a provisioned principal.</returns>
    ValueTask<string?> ResolvePrincipalIdOrDefaultAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the stable principal identifier for a caller, provisioning a principal (and its connection identity)
    /// on first sight. Used by write paths (such as recording resource ownership) that must attribute an action to a
    /// durable principal.
    /// </summary>
    /// <param name="user">The authenticated caller.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the provisioning participates in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// principal identifier.</returns>
    ValueTask<string> ResolvePrincipalIdAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
