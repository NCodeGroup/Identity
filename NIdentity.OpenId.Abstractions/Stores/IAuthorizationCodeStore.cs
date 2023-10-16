#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Stores;

/// <summary>
/// Provides an abstraction for a store which manages <see cref="AuthorizationCode"/> instances.
/// </summary>
public interface IAuthorizationCodeStore : IStore<AuthorizationCode>
{
    /// <summary>
    /// Removes an <c>Authentication Code</c> grant from the store using its hashed representation.
    /// </summary>
    /// <param name="hashedCode">The hashed representation of an <c>Authentication Code</c> grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveByHashedCodeAsync(string hashedCode, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get an <see cref="AuthorizationCode"/> instance by using the hashed representation of an <c>Authentication Code</c> grant.
    /// </summary>
    /// <param name="hashedCode">The hashed representation of an <c>Authentication Code</c> grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="AuthorizationCode"/> instance matching the specified <paramref name="hashedCode"/> if it exists.</returns>
    ValueTask<AuthorizationCode?> TryGetByHashedCodeAsync(string hashedCode, CancellationToken cancellationToken);
}
