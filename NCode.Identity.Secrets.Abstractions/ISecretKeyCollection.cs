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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides a read-only collection of <see cref="SecretKey"/> instances that can be accessed by <c>Key ID (KID)</c>.
/// The collection is sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
/// </summary>
[PublicAPI]
public interface ISecretKeyCollection : IReadOnlyCollection<SecretKey>
{
    /// <summary>
    /// Attempts to get a <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> of the <see cref="SecretKey"/> to get.</param>
    /// <param name="secretKey">When this method returns, the <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>,
    /// if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a <see cref="SecretKey"/> was found with the specified <c>Key ID (KID)</c>; otherwise, <c>false</c>.</returns>
    bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey);
}
