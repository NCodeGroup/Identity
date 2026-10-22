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

using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey.Facades;

/// <summary>
/// Provides the database of the one connection a process holds to the Valkey server, so the code that issues commands
/// can be tested without a server.
/// </summary>
internal interface IValkeyConnection
{
    /// <summary>
    /// Gets the database to issue commands against, connecting on first use.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="IDatabase"/> to issue commands against.</returns>
    ValueTask<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the subscriber of the one connection, connecting on first use.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ISubscriber"/> to publish and subscribe with.</returns>
    ValueTask<ISubscriber> GetSubscriberAsync(CancellationToken cancellationToken);
}
