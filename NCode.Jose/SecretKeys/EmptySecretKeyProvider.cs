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
using NCode.Jose.Infrastructure;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides an <see cref="ISecretKeyProvider"/> implementation that is empty.
/// </summary>
public sealed class EmptySecretKeyProvider : ISecretKeyProvider
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptySecretKeyProvider"/>.
    /// </summary>
    public static EmptySecretKeyProvider Singleton { get; } = new();

    /// <inheritdoc />
    public ISecretKeyCollection Collection => EmptySecretKeyCollection.Singleton;

    private EmptySecretKeyProvider()
    {
        // nothing
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // nothing
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
