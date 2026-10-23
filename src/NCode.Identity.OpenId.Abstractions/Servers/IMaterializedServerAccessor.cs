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

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides access to the <see cref="OpenIdServer"/> that this replica materialized, for background work that runs
/// outside any request.
/// </summary>
[PublicAPI]
public interface IMaterializedServerAccessor
{
    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> that this replica materialized, waiting until the replica materializes it at
    /// its first request, so background work never reads the store while the host starts.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to stop waiting.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the materialized <see cref="OpenIdServer"/>.</returns>
    ValueTask<OpenIdServer> GetServerAsync(CancellationToken cancellationToken);
}
