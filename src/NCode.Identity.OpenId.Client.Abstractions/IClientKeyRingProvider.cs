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
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides the credentials of a client from a named key ring.
/// </summary>
/// <remarks>
/// A key ring names a keyed <c>ISecretKeyCollectionProvider</c>, so the keys can rotate and can live in a KMS, and a
/// store-backed source can replace this seam.
/// </remarks>
[PublicAPI]
public interface IClientKeyRingProvider
{
    /// <summary>
    /// Gets the keys of the specified key ring.
    /// </summary>
    /// <param name="keyRing">The name of the key ring.</param>
    /// <exception cref="InvalidOperationException">The key ring is not registered.</exception>
    ISecretKeyCollection GetKeyRing(string keyRing);
}
