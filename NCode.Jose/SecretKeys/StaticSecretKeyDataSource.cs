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

using Microsoft.Extensions.Primitives;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides an implementation of <see cref="ISecretKeyDataSource"/> that uses a static collection of <see cref="SecretKey"/> instances.
/// </summary>
public sealed class StaticSecretKeyDataSource : ISecretKeyDataSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaticSecretKeyDataSource"/> class.
    /// </summary>
    /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances.</param>
    public StaticSecretKeyDataSource(IEnumerable<SecretKey> secretKeys)
    {
        SecretKeys = secretKeys.ToList();
    }

    /// <inheritdoc />
    public IEnumerable<SecretKey> SecretKeys { get; }

    /// <inheritdoc />
    public void Dispose() => SecretKeys.DisposeAll();

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
