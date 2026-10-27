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

namespace NCode.Identity.SharedState;

/// <summary>
/// The adapter port for expiring sets of member keys under an index key that every replica of a deployment observes, so
/// a caller finds every record for an attribute, such as every session of one OP <c>sid</c>, without scanning. Each
/// member expires on its own; the index is present while any member is. The default adapter is in-process and therefore
/// correct only for a single replica; a deployment that runs several replicas registers a shared adapter (for example
/// Valkey) so a member added by one replica is seen by all of them.
/// </summary>
// See ADR-0267.
[PublicAPI]
public interface IExpiringIndexSet
{
    /// <summary>
    /// Adds <paramref name="member"/> to the index named <paramref name="indexKey"/>, or sets the expiry of the member
    /// when it is already present.
    /// </summary>
    /// <param name="indexKey">The name of the index. Keys are compared ordinally.</param>
    /// <param name="member">The member to add. Members are compared ordinally.</param>
    /// <param name="expiresWhen">The instant after which the member is no longer present. An instant that is not in the future adds nothing.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the member has been added.</returns>
    ValueTask AddMemberAsync(
        string indexKey,
        string member,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the unexpired members of the index named <paramref name="indexKey"/>, without changing it.
    /// </summary>
    /// <param name="indexKey">The name of the index. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The unexpired members in no particular order; or an empty list if the index is absent.</returns>
    ValueTask<IReadOnlyList<string>> GetMembersAsync(
        string indexKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes <paramref name="member"/> from the index named <paramref name="indexKey"/>. Removing a member that is
    /// absent does nothing.
    /// </summary>
    /// <param name="indexKey">The name of the index. Keys are compared ordinally.</param>
    /// <param name="member">The member to remove. Members are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the member is no longer present.</returns>
    ValueTask RemoveMemberAsync(
        string indexKey,
        string member,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the index named <paramref name="indexKey"/> and every member. Removing an index that is absent does
    /// nothing.
    /// </summary>
    /// <param name="indexKey">The name of the index. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the index is no longer present.</returns>
    ValueTask RemoveAsync(string indexKey, CancellationToken cancellationToken);
}
