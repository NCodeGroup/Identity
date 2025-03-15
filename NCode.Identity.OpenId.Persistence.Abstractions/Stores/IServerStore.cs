#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.Stores;
/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedServer"/> instances.
/// </summary>
[PublicAPI]
public interface IServerStore : IStore<PersistedServer>
{
    /// <summary>
    /// Attempts to get a <see cref="PersistedServer"/> instance by using its natural key.
    /// </summary>
    /// <param name="serverId">The natural key of the <see cref="PersistedServer"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedServer"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedServer?> TryGetByServerIdAsync(string serverId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the <see cref="JsonElement"/> settings for the specified <paramref name="serverId"/>.
    /// The concurrency token from <paramref name="lastKnownState"/> is used to check if the current value has changed.
    /// If the value hasn't changed, this method returns the same <paramref name="lastKnownState"/> instance;
    /// otherwise, it returns the most recent value from the store.
    /// </summary>
    /// <param name="serverId">The natural key of the entity.</param>
    /// <param name="lastKnownState">The last known state of the <see cref="JsonElement"/> settings for the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="JsonElement"/> settings for the specified entity.</returns>
    ValueTask<ConcurrentState<JsonElement>> GetSettingsAsync(
        string serverId,
        ConcurrentState<JsonElement> lastKnownState,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the <see cref="PersistedSecret"/> collection for the specified <paramref name="serverId"/>.
    /// The concurrency token from <paramref name="lastKnownState"/> is used to check if the current value has changed.
    /// If the value hasn't changed, this method returns the same <paramref name="lastKnownState"/> instance;
    /// otherwise, it returns the most recent value from the store.
    /// </summary>
    /// <param name="serverId">The natural key of the entity.</param>
    /// <param name="lastKnownState">The last known state of the <see cref="PersistedSecret"/> collection for the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the collection of  <see cref="PersistedSecret"/> instances for the specified entity.</returns>
    ValueTask<ConcurrentState<IReadOnlyCollection<PersistedSecret>>> GetSecretsAsync(
        string serverId,
        ConcurrentState<IReadOnlyCollection<PersistedSecret>> lastKnownState,
        CancellationToken cancellationToken
    );
}
